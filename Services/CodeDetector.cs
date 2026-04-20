using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clipboarder.Services;

// Classifies a string as code + language. The scoring approach: run cheap per-language
// signal probes, weight unique tokens higher than shared ones, take the top scorer over
// a minimum threshold. A handful of "strong signatures" short-circuit the scoring when
// a language is effectively unambiguous (shebang, <?php, <!DOCTYPE, JSON/XML prefix, etc.).
public static class CodeDetector
{
    public readonly record struct Result(bool IsCode, string? Lang);

    private const int LanguageThreshold = 4;   // min score to call a snippet "Lang X code"
    private const int GenericThreshold  = 3;   // min score for an unknown-language code fallback

    public static Result Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return default;
        var t = text.Trim();
        if (t.Length < 2) return default;

        if (t.StartsWith("#!"))
        {
            var line1 = t.Split('\n', 2)[0];
            if (line1.Contains("python")) return new(true, "py");
            if (line1.Contains("node"))   return new(true, "js");
            if (line1.Contains("ruby"))   return new(true, "rb");
            if (line1.Contains("pwsh") || line1.Contains("powershell")) return new(true, "ps1");
            return new(true, "sh");
        }
        if (t.StartsWith("<?php", StringComparison.OrdinalIgnoreCase)) return new(true, "php");
        if (Regex.IsMatch(t, @"^\s*<\?xml\b", RegexOptions.IgnoreCase)) return new(true, "xml");
        if (Regex.IsMatch(t, @"^\s*<!DOCTYPE\s+html", RegexOptions.IgnoreCase)) return new(true, "html");
        if (Regex.IsMatch(t, @"^\s*@startuml\b", RegexOptions.IgnoreCase)) return new(true, "puml");

        // JSON — confirmed by a real parse.
        if (LooksLikeJson(t)) return new(true, "json");

        // HTML/XML — must start with an opening tag AND contain a matching close or self-close.
        if (Regex.IsMatch(t, @"^\s*<[a-zA-Z][\w:.-]*[\s>/]"))
        {
            if (LooksLikeHtml(t)) return new(true, "html");
            if (t.Contains("</") || Regex.IsMatch(t, @"<[^>]+/\s*>")) return new(true, "xml");
        }

        // Dockerfile instructions at start of a line (multi-instruction → likely Dockerfile).
        if (CountDockerfileInstructions(t) >= 2) return new(true, "dockerfile");

        // Single-line terminal command (git/npm/docker/ssh/etc.)
        if (IsSingleLineShellCommand(t)) return new(true, "sh");

        var s = new Dictionary<string, int>(StringComparer.Ordinal);
        ScoreJs(t, s);
        ScoreTs(t, s);
        ScorePython(t, s);
        ScoreCSharp(t, s);
        ScoreJava(t, s);
        ScoreGo(t, s);
        ScoreRust(t, s);
        ScoreCpp(t, s);
        ScoreRuby(t, s);
        ScorePhp(t, s);
        ScoreSql(t, s);
        ScoreCss(t, s);
        ScoreYaml(t, s);
        ScoreMarkdown(t, s);
        ScoreBash(t, s);
        ScorePowerShell(t, s);
        ScoreKotlin(t, s);
        ScoreSwift(t, s);
        ScoreLua(t, s);
        ScoreHaskell(t, s);
        ScoreR(t, s);
        ScoreScala(t, s);
        ScoreXml(t, s);

        var winner = s.Where(kv => kv.Value >= LanguageThreshold)
                      .OrderByDescending(kv => kv.Value)
                      .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                      .FirstOrDefault();
        if (winner.Value >= LanguageThreshold) return new(true, winner.Key);

        if (GenericCodeScore(t) >= GenericThreshold) return new(true, "txt");

        return default;
    }

    private static bool LooksLikeJson(string t)
    {
        if (t.Length < 2) return false;
        var c = t[0];
        if (c != '{' && c != '[') return false;
        try { using var _ = JsonDocument.Parse(t); return true; }
        catch { return false; }
    }

    private static bool LooksLikeHtml(string t)
    {
        // Any well-known tag (case-insensitive).
        string[] tags = {
            "html","head","body","div","span","p","a","img","ul","ol","li","table","tr","td","th",
            "h1","h2","h3","h4","h5","h6","form","input","button","label","select","option","script",
            "style","link","meta","header","footer","nav","section","article","main","aside","figure"
        };
        return tags.Any(tag => Regex.IsMatch(t, @"<" + tag + @"\b", RegexOptions.IgnoreCase));
    }

    private static int CountDockerfileInstructions(string t)
    {
        string[] ins = { "FROM", "RUN", "CMD", "LABEL", "MAINTAINER", "EXPOSE", "ENV", "ADD",
                         "COPY", "ENTRYPOINT", "VOLUME", "USER", "WORKDIR", "ARG", "ONBUILD",
                         "STOPSIGNAL", "HEALTHCHECK", "SHELL" };
        int n = 0;
        foreach (var line in t.Split('\n'))
        {
            var trimmed = line.TrimStart();
            foreach (var i in ins)
                if (trimmed.StartsWith(i + " ", StringComparison.Ordinal)) { n++; break; }
        }
        return n;
    }

    private static bool IsSingleLineShellCommand(string t)
    {
        if (t.Contains('\n')) return false;
        return Regex.IsMatch(t, @"^\s*(sudo\s+)?(git|npm|yarn|pnpm|bun|deno|npx|node|python3?|py|pip3?|pipx|poetry|uv|cargo|rustc|rustup|go|gofmt|mvn|gradle|dotnet|msbuild|adb|docker|docker-compose|podman|kubectl|helm|terraform|tf|aws|gcloud|az|heroku|vercel|netlify|ssh|scp|rsync|curl|wget|ping|netstat|ss|ip|ifconfig|traceroute|nmap|tar|zip|unzip|gzip|gunzip|bzip2|xz|apt|apt-get|yum|dnf|pacman|brew|choco|winget|scoop|systemctl|service|journalctl|ps|top|htop|kill|killall|nohup|bg|fg|jobs|cd|ls|dir|pwd|mkdir|rmdir|rm|cp|mv|ln|cat|less|more|head|tail|grep|egrep|fgrep|awk|sed|sort|uniq|cut|tr|wc|diff|patch|chmod|chown|chgrp|touch|stat|file|which|whereis|locate|find|xargs|tee|echo|printf|export|source|alias|history|clear|make|cmake|ninja|gcc|g\+\+|clang|javac|java)\b");
    }

    // Each per-language scorer adds up signals unique-to-shared; the threshold
    // at the top of Detect filters out noise.
    private static void ScoreJs(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\b(const|let|var)\s+\w+\s*=")) score += 3;
        if (R(t, @"\bfunction\s*\w*\s*\(")) score += 3;
        if (R(t, @"=>\s*[{(]?")) score += 2;
        if (R(t, @"\bconsole\.\w+\s*\(")) score += 4;
        if (R(t, @"\brequire\s*\(\s*['""]")) score += 3;
        if (R(t, @"\bimport\s+[\w{},\s*]+\s+from\s+['""]")) score += 3;
        if (R(t, @"\bexport\s+(default|const|function|class)\b")) score += 3;
        if (R(t, @"\b(async|await)\b")) score += 2;
        if (R(t, @"\bdocument\.(getElementById|querySelector|createElement)\b")) score += 4;
        if (R(t, @"\bwindow\.\w+")) score += 2;
        if (R(t, @"===|!==|&&|\|\|")) score += 1;
        if (R(t, @"\bnew\s+[A-Z]\w+\s*\(")) score += 2;
        if (R(t, @"//[^\n]*")) score += 1;
        if (CountEndsWithSemicolon(t) >= 2) score += 2;
        s["js"] = score;
    }

    private static void ScoreTs(string t, Dictionary<string, int> s)
    {
        // TS is JS + type annotations. We only award points for TS-specific features,
        // then add the JS baseline so strong TS always beats strong JS.
        int score = 0;
        if (R(t, @":\s*(string|number|boolean|any|unknown|never|void|object)\b")) score += 4;
        if (R(t, @"\binterface\s+\w+\s*\{")) score += 4;
        if (R(t, @"\btype\s+\w+\s*=\s*")) score += 3;
        if (R(t, @"\benum\s+\w+\s*\{")) score += 3;
        if (R(t, @"\bas\s+(string|number|const|\w+\[\])\b")) score += 2;
        if (R(t, @"<\w+(,\s*\w+)*>\s*\(")) score += 2;   // generic call
        if (R(t, @"\b(public|private|protected|readonly)\s+\w+\s*:")) score += 3;
        if (R(t, @"\bpartial\b|\bRecord<\w+")) score += 2;
        if (score > 0 && s.TryGetValue("js", out var js)) score += Math.Min(js, 8);
        s["ts"] = score;
    }

    private static void ScorePython(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"^\s*def\s+\w+\s*\(.*\)\s*:", Multi)) score += 4;
        if (R(t, @"^\s*class\s+\w+(\([^)]*\))?\s*:", Multi)) score += 4;
        if (R(t, @"^\s*(from|import)\s+[\w.]+", Multi)) score += 3;
        if (R(t, @"\bprint\s*\(")) score += 3;
        if (R(t, @"\bif\s+[^:]+:\s*$", Multi)) score += 2;
        if (R(t, @"\b(elif|lambda|yield|pass|self|None|True|False)\b")) score += 2;
        if (R(t, @"\bf['""][^'""]*\{[^}]+\}[^'""]*['""]")) score += 3;   // f-string
        if (R(t, @"^\s*@\w+(\.\w+)*\s*$", Multi)) score += 2;           // decorators
        if (R(t, @"#[^\n]*")) score += 1;
        if (R(t, @"->\s*[A-Za-z_]\w*")) score += 2;
        if (NoSemicolonEnds(t) && t.Contains(':')) score += 1;
        s["py"] = score;
    }

    private static void ScoreCSharp(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\busing\s+[\w.]+\s*;")) score += 4;
        if (R(t, @"\bnamespace\s+[\w.]+\s*[{;]")) score += 4;
        if (R(t, @"\b(public|private|internal|protected)\s+(static\s+)?(async\s+)?\w+(<[\w,\s]+>)?\s+\w+\s*\(")) score += 4;
        if (R(t, @"\b(string|int|bool|double|float|decimal|var|object|dynamic)\s+\w+\s*=")) score += 3;
        if (R(t, @"\bConsole\.(Write(Line)?|Read(Line)?)\s*\(")) score += 4;
        if (R(t, @"\bTask<?\w*>?\b|\basync\s+\w+|\bawait\s+\w+")) score += 3;
        if (R(t, @"\bnew\s+[A-Z]\w*\s*\(")) score += 2;
        if (R(t, @"\bget\s*;\s*set\s*;")) score += 4;
        if (R(t, @"=>\s*\w+\s*;")) score += 2;                          // expression-bodied
        if (R(t, @"\[[\w.]+(\([^)]*\))?\]")) score += 1;               // attributes
        if (CountEndsWithSemicolon(t) >= 2) score += 1;
        s["cs"] = score;
    }

    private static void ScoreJava(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bpublic\s+(static\s+)?(final\s+)?class\s+\w+")) score += 4;
        if (R(t, @"\bpackage\s+[\w.]+\s*;")) score += 4;
        if (R(t, @"\bimport\s+[\w.]+(\.\*)?\s*;")) score += 3;
        if (R(t, @"\bSystem\.out\.(println?|printf)\s*\(")) score += 5;
        if (R(t, @"\b(public|private|protected)\s+(static\s+)?(final\s+)?(void|String|int|boolean|double|float|long|char)\s+\w+\s*\(")) score += 4;
        if (R(t, @"@(Override|Deprecated|SuppressWarnings|Autowired|Component|Service|RestController|GetMapping|PostMapping)\b")) score += 3;
        if (R(t, @"\bextends\s+\w+|\bimplements\s+\w+")) score += 2;
        if (R(t, @"\bnew\s+\w+<[\w,\s<>]*>\s*\(")) score += 2;
        if (CountEndsWithSemicolon(t) >= 2) score += 1;
        s["java"] = score;
    }

    private static void ScoreGo(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bpackage\s+\w+\s*$", Multi)) score += 4;
        if (R(t, @"\bfunc\s+(\(\s*\w+\s+\*?\w+\s*\)\s*)?\w+\s*\(")) score += 4;
        if (R(t, @"\bimport\s*\(([^)]+)\)")) score += 3;
        if (R(t, @":=\s*")) score += 3;
        if (R(t, @"\bfmt\.(Print(ln|f)?|Scan)\s*\(")) score += 5;
        if (R(t, @"\b(chan|go|defer|goroutine|interface\s*\{|struct\s*\{)\b")) score += 3;
        if (R(t, @"\bmap\[\w+\]\w+")) score += 3;
        if (R(t, @"\berror\s*$|return\s+nil\s*,?\s*err\b", Multi)) score += 2;
        if (NoSemicolonEnds(t) && t.Contains('{')) score += 1;
        s["go"] = score;
    }

    private static void ScoreRust(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bfn\s+\w+\s*(<[^>]+>)?\s*\(")) score += 4;
        if (R(t, @"\blet\s+(mut\s+)?\w+\s*(:[^=]+)?=")) score += 3;
        if (R(t, @"\bimpl\s+(<[^>]+>\s+)?\w+")) score += 4;
        if (R(t, @"\btrait\s+\w+|\bstruct\s+\w+|\benum\s+\w+")) score += 3;
        if (R(t, @"\bprintln!\s*\(|\bprint!\s*\(|\bdbg!\s*\(|\bvec!\s*\[|\bformat!\s*\(|\bpanic!\s*\(")) score += 5;
        if (R(t, @"->\s*\w+(<[^>]+>)?")) score += 2;
        if (R(t, @"\bmatch\s+\w+\s*\{")) score += 3;
        if (R(t, @"&\s*(mut\s+)?\w+")) score += 1;
        if (R(t, @"\buse\s+[\w:]+(::\{[^}]+\})?\s*;")) score += 3;
        s["rs"] = score;
    }

    private static void ScoreCpp(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"#include\s*<[^>]+>")) score += 5;
        if (R(t, @"\bstd::\w+")) score += 4;
        if (R(t, @"\b(cout|cin|endl)\b")) score += 4;
        if (R(t, @"\b(void|int|bool|char|double|float|auto)\s+\w+\s*\([^)]*\)\s*\{", Multi)) score += 3;
        if (R(t, @"\btemplate\s*<[^>]+>")) score += 4;
        if (R(t, @"\b(class|struct|namespace)\s+\w+")) score += 2;
        if (R(t, @"->(\w+|\*)")) score += 2;
        if (R(t, @"::[A-Z_][\w]*")) score += 2;
        if (CountEndsWithSemicolon(t) >= 2) score += 1;
        s["cpp"] = score;
    }

    private static void ScoreRuby(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"^\s*def\s+\w+[!?]?\s*(\([^)]*\))?\s*$", Multi)) score += 4;
        if (R(t, @"^\s*end\s*$", Multi)) score += 3;
        if (R(t, @"\bclass\s+\w+(\s*<\s*\w+)?\s*$", Multi)) score += 3;
        if (R(t, @"\b(puts|print|p|require|require_relative)\b")) score += 3;
        if (R(t, @"\bdo\s*\|[^|]*\|")) score += 3;
        if (R(t, @":\w+\s*=>\s*")) score += 3;                            // symbol hash rocket
        if (R(t, @"@\w+")) score += 2;
        if (R(t, @"#[^\n]*")) score += 1;
        s["rb"] = score;
    }

    private static void ScorePhp(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (t.Contains("<?php") || t.Contains("<?=")) score += 5;
        if (R(t, @"\$\w+")) score += 3;
        if (R(t, @"->\w+\s*\(")) score += 2;
        if (R(t, @"::\w+\s*\(")) score += 2;
        if (R(t, @"\b(echo|print_r|var_dump|isset|array|foreach|use|namespace)\b")) score += 2;
        if (R(t, @"=>\s*")) score += 2;
        if (CountEndsWithSemicolon(t) >= 2) score += 1;
        s["php"] = score;
    }

    private static void ScoreSql(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bSELECT\s+[\w\*,\s]+\s+FROM\s+\w", RegexOptions.IgnoreCase)) score += 6;
        if (R(t, @"\bINSERT\s+INTO\s+\w+", RegexOptions.IgnoreCase)) score += 5;
        if (R(t, @"\bUPDATE\s+\w+\s+SET\b", RegexOptions.IgnoreCase)) score += 5;
        if (R(t, @"\bDELETE\s+FROM\s+\w+", RegexOptions.IgnoreCase)) score += 5;
        if (R(t, @"\bCREATE\s+(TABLE|INDEX|VIEW|PROCEDURE|FUNCTION|DATABASE|SCHEMA)\b", RegexOptions.IgnoreCase)) score += 5;
        if (R(t, @"\b(WHERE|JOIN|INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|OFFSET)\b", RegexOptions.IgnoreCase)) score += 2;
        if (R(t, @"\bALTER\s+TABLE\b", RegexOptions.IgnoreCase)) score += 4;
        s["sql"] = score;
    }

    private static void ScoreCss(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"[.#]?[\w-]+\s*\{[^}]*(:[^;}]+;)", Multi)) score += 4;
        if (R(t, @"\b(color|background|margin|padding|font-size|display|flex|grid|width|height|border)\s*:")) score += 3;
        if (R(t, @"@(media|keyframes|import|supports|font-face)\b")) score += 3;
        if (R(t, @":\s*-?\d+(\.\d+)?(px|rem|em|vh|vw|%|ch)\b")) score += 3;
        if (R(t, @"#[0-9a-fA-F]{3,8}\b")) score += 1;
        if (R(t, @"\b(hover|focus|active|disabled|first-child|last-child|nth-child)\b")) score += 2;
        s["css"] = score;
    }

    private static void ScoreYaml(string t, Dictionary<string, int> s)
    {
        if (!t.Contains(':')) { s["yaml"] = 0; return; }
        int score = 0;
        if (R(t, @"^\s*[-?]\s+\w", Multi)) score += 2;
        if (R(t, @"^\s*\w[\w-]*:\s*(\S.*)?$", Multi)) score += 3;
        if (R(t, @"^\s*-\s+\w[\w-]*:", Multi)) score += 2;
        if (R(t, @"^\s*---\s*$", Multi)) score += 4;                      // doc separator
        if (t.Contains('{') || t.Contains('}')) score -= 2;              // YAML rarely has bare braces
        if (t.Contains(';')) score -= 2;                                  // YAML has no semicolons
        s["yaml"] = Math.Max(score, 0);
    }

    private static void ScoreMarkdown(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"^#{1,6}\s+\S", Multi)) score += 3;
        if (R(t, @"^\s*[-*+]\s+\S", Multi)) score += 2;
        if (R(t, @"^\s*\d+\.\s+\S", Multi)) score += 2;
        if (R(t, @"^```[\w-]*\s*$", Multi)) score += 4;                  // fenced block
        if (R(t, @"\*\*[^*\n]+\*\*|\*[^*\n]+\*|_[^_\n]+_")) score += 2;
        if (R(t, @"\[[^\]]+\]\([^)]+\)")) score += 3;                     // [text](url)
        if (R(t, @"^>\s+\S", Multi)) score += 1;
        s["md"] = score;
    }

    private static void ScoreBash(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\$\{?\w+\}?")) score += 2;
        if (R(t, @"\b(if|then|else|elif|fi|for|do|done|while|case|esac|function)\b")) score += 2;
        if (R(t, @"\[\[\s+.*\s+\]\]")) score += 3;
        if (R(t, @"\|\s*(grep|awk|sed|xargs|sort|uniq|cut|head|tail)\b")) score += 4;
        if (R(t, @"2>&1|>/dev/null|&&|\|\|")) score += 2;
        if (R(t, @"^\s*\w+=[^=\s]", Multi)) score += 2;                   // VAR=value
        if (R(t, @"\bexport\s+\w+=")) score += 3;
        s["sh"] = score;
    }

    private static void ScorePowerShell(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\b[A-Z][a-z]+-[A-Z][a-z]\w+\b")) score += 4;           // Verb-Noun cmdlets
        if (R(t, @"\$\w+\s*=")) score += 3;
        if (R(t, @"\|\s*(Where|Select|ForEach|Sort|Group|Measure|Out-)")) score += 4;
        if (R(t, @"-(eq|ne|gt|lt|ge|le|like|match|contains)\b")) score += 3;
        if (R(t, @"\[(int|string|bool|array|hashtable)\]")) score += 2;
        s["ps1"] = score;
    }

    private static void ScoreKotlin(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bfun\s+\w+\s*\(")) score += 4;
        if (R(t, @"\bval\s+\w+|\bvar\s+\w+")) score += 3;
        if (R(t, @"\bprintln\s*\(")) score += 3;
        if (R(t, @"\bdata\s+class\b|\bobject\s+\w+|\bsealed\s+class\b")) score += 4;
        if (R(t, @":\s*\w+\??\s*=")) score += 1;
        if (R(t, @"\bwhen\s*\(")) score += 3;
        s["kt"] = score;
    }

    private static void ScoreSwift(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bfunc\s+\w+\s*\(")) score += 3;
        if (R(t, @"\blet\s+\w+|\bvar\s+\w+")) score += 2;
        if (R(t, @"\bguard\s+.+else\b")) score += 4;
        if (R(t, @"\bimport\s+(Foundation|SwiftUI|UIKit|Combine)\b")) score += 5;
        if (R(t, @"\bprint\s*\(")) score += 2;
        if (R(t, @"->\s*\w+")) score += 2;
        s["swift"] = score;
    }

    private static void ScoreLua(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\blocal\s+\w+")) score += 3;
        if (R(t, @"\bfunction\s+\w+[.:]\w+\s*\(|\bfunction\s+\w+\s*\(")) score += 3;
        if (R(t, @"^\s*end\s*$", Multi)) score += 2;
        if (R(t, @"\b(then|do|repeat|until|nil)\b")) score += 2;
        if (R(t, @"\bprint\s*\(")) score += 1;
        if (R(t, @"--\[\[|--[^\n]*")) score += 2;
        s["lua"] = score;
    }

    private static void ScoreHaskell(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"^\s*\w+\s*::\s+[\w()\[\]\s>'-]+", Multi)) score += 4;
        if (R(t, @"\bmodule\s+\w+(\.\w+)*\s+where\b")) score += 4;
        if (R(t, @"->\s+\w+|=>\s+\w+")) score += 2;
        if (R(t, @"\b(do|case|of|let|in|where|data|newtype|type|deriving|instance|class)\b")) score += 2;
        if (R(t, @"<-\s*")) score += 2;
        s["hs"] = score;
    }

    private static void ScoreR(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"<-\s*")) score += 3;
        if (R(t, @"\b(library|require|install\.packages|source)\s*\(")) score += 4;
        if (R(t, @"\b(c|data\.frame|list|as\.\w+|summary|print|head|tail|nrow|ncol|mean|median)\s*\(")) score += 2;
        if (R(t, @"%>%|%\|%")) score += 3;
        s["r"] = score;
    }

    private static void ScoreXml(string t, Dictionary<string, int> s)
    {
        int score = 0;
        var tagOpens = Regex.Matches(t, @"<[A-Za-z][\w.:-]*(\s+[\w.:-]+\s*=\s*""[^""]*"")*\s*/?>").Count;
        var tagCloses = Regex.Matches(t, @"</[A-Za-z][\w.:-]*\s*>").Count;
        if (tagOpens >= 2) score += 4;
        if (tagCloses >= 1) score += 3;
        if (tagOpens + tagCloses >= 4) score += 2;
        if (Regex.IsMatch(t, @"xmlns(:\w+)?\s*=\s*""")) score += 4;
        if (Regex.IsMatch(t, @"\{\w+\s+[^}]+\}|\{StaticResource\s+\w+\}|\{Binding\b")) score += 3;
        s["xml"] = score;
    }

    private static void ScoreScala(string t, Dictionary<string, int> s)
    {
        int score = 0;
        if (R(t, @"\bdef\s+\w+\s*(\[[^\]]+\])?\s*\(")) score += 3;
        if (R(t, @"\bobject\s+\w+|\btrait\s+\w+|\bcase\s+class\b")) score += 4;
        if (R(t, @"\bval\s+\w+|\bvar\s+\w+")) score += 2;
        if (R(t, @"\bprintln\s*\(")) score += 2;
        if (R(t, @"->|=>|<-")) score += 1;
        s["scala"] = score;
    }

    // Programming-only signals — punctuation patterns, operators, and structures
    // that are extremely rare in natural-language prose. Each group contributes
    // modestly so the threshold is only reached when several independent code-like
    // features overlap.
    private static int GenericCodeScore(string t)
    {
        int score = 0;
        bool multi = t.Contains('\n');
        int opens  = t.Count(c => c == '{') + t.Count(c => c == '[') + t.Count(c => c == '(');
        int closes = t.Count(c => c == '}') + t.Count(c => c == ']') + t.Count(c => c == ')');
        int semis  = t.Count(c => c == ';');

        // Balanced bracket structure across lines — strong structural signal.
        if (multi && opens >= 2 && closes >= 2 && Math.Abs(opens - closes) <= 2) score += 2;
        if (semis >= 2) score += 2;
        if (R(t, @"^\s{2,}\S", Multi)) score += 1;                       // nested indentation
        if (R(t, @"//[^\n]*|/\*[\s\S]*?\*/")) score += 1;                // //... /* ... */
        if (R(t, @"(?m)^\s*#\s+\S")) score += 1;                         // # hash-comment lines
        if (R(t, @"\b[A-Z][a-zA-Z0-9]+\.[a-z]\w+\s*\(")) score += 1;     // Foo.bar(

        // Operators that are effectively exclusive to source code.
        if (R(t, @"===|!==|&&|\|\||=>|->|::|\?\?|\?\.|<=|>=|\+\+|<-")) score += 2;

        // Three or more function-call shapes: name(args…).
        if (Regex.Matches(t, @"\b[A-Za-z_]\w*\s*\([^()\n]{0,200}\)").Count >= 3) score += 1;

        // Chained method calls: .foo().bar(
        if (R(t, @"\.\w+\s*\([^()\n]*\)\s*\.\w+\s*\(")) score += 1;

        // Repeated "key: value" lines — JSON/YAML/config style.
        if (multi && Regex.Matches(t,
                @"(?m)^\s*""?[\w.-]+""?\s*:\s*[^:\s][^:\n]{0,200}$").Count >= 2) score += 1;

        // Assignment-looking lines: name = expr  (skip prose-heavy "X = Y" fluke by requiring 2+).
        if (multi && Regex.Matches(t, @"(?m)^\s*[\w.]+\s*=\s*\S").Count >= 2) score += 1;

        return score;
    }

    private const RegexOptions Multi = RegexOptions.Multiline;

    private static bool R(string t, string pattern, RegexOptions opts = RegexOptions.None)
        => Regex.IsMatch(t, pattern, opts);

    private static int CountEndsWithSemicolon(string t)
    {
        int n = 0;
        foreach (var line in t.Split('\n'))
            if (line.TrimEnd().EndsWith(";")) n++;
        return n;
    }

    private static bool NoSemicolonEnds(string t)
        => CountEndsWithSemicolon(t) == 0;
}
