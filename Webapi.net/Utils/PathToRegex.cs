using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace Webapi.net
{
    /// <summary>
    /// This is a port of path-to-regexp.
    /// https://github.com/pillarjs/path-to-regexp
    /// 
    /// MIT License. Original JS version written by Blake Embrey. 
    /// </summary>
    public static class PathToRegexUtil
    {
        static private Regex PATH_REGEX = new Regex(
            // Match escaped characters that would otherwise appear in future matches.
            // This allows the user to escape special characters that won't transform.
            "(\\\\.)" +

            "|" +

            // Match Express-style parameters and un-named parameters with a prefix
            // and optional suffixes. Matches appear as:
            //
            // ":test(\\d+)?" => ["test", "\d+", undefined, "?"]
            // "(\\d+)"  => [undefined, undefined, "\d+", undefined]
            "(?:\\:(\\w+)(?:\\(((?:\\\\.|[^\\\\()])+)\\))?|\\(((?:\\\\.|[^\\\\()])+)\\))([+*?])?"
            ,
            RegexOptions.ECMAScript | RegexOptions.Compiled);

        private const char DEFAULT_DELIMITER = '/';

        public class PathToRegexOptions
        {
            /// <summary>
            /// When true the regex will be case sensitive. (default: false)
            /// </summary>
            public bool Sensitive = false;

            /// <summary>
            /// When false the regex allows an optional trailing delimiter to match. (default: false)
            /// </summary>
            public bool Strict = false;

            /// <summary>
            /// When true the regex will match from the beginning of the string. (default: true)
            /// </summary>
            public bool Start = true;

            /// <summary>
            /// When true the regex will match to the end of the string. (default: true)
            /// </summary>
            public bool End = true;

            /// <summary>
            /// The default delimiter for segments. (default: '/')
            /// </summary>
            public char Delimiter = DEFAULT_DELIMITER;

            /// <summary>
            /// Optional character, or list of characters, to treat as "end" characters.
            /// </summary>
            public char[] EndsWith = null;

            /// <summary>
            /// List of characters to consider delimiters when parsing. (default: undefined, any character)
            /// </summary>
            public string Whitelist = null;
        }

        public class Token
        {
            public string Raw;
            public int? Index;
            public string Name;
            public string Prefix;
            public char? Delimiter;
            public bool Optional;
            public bool Repeat;
            public string Pattern;
        }

        /**
         * Parse a string for the raw tokens.
         *
         * @param  {string}  str
         * @param  {Object=} options
         * @return {!Array}
         */
        static List<Token> Parse(string str, PathToRegexOptions options = null)
        {
            options = options ?? new PathToRegexOptions();

            var tokens = new List<Token>();
            var key = 0;
            var index = 0;
            var path = "";
            var pathEscaped = false;

            var res = PATH_REGEX.Matches(str);

            foreach (Match match in res)
            {
                var m = match.Value;
                var escaped = match.Groups[1];
                var offset = match.Index;
                path += str.Substring(index, offset - index);
                index = offset + m.Length;

                // Ignore already escaped sequences.
                if (escaped.Captures.Count > 0)
                {
                    path += escaped.Value;
                    pathEscaped = true;
                    continue;
                }

                var prev = "";
                var name = match.Groups[2].Value;
                var capture = match.Groups[3].Value;
                var group = match.Groups[4].Value;
                var modifier = match.Groups[5].Value;

                if (!pathEscaped && path.Length > 0)
                {
                    var k = path.Length - 1;
                    var c = path[k];
                    var matches = options.Whitelist != null ? options.Whitelist.Contains(c) : true;

                    if (matches)
                    {
                        prev = c.ToString();
                        path = path.Substring(0, k);
                    }
                }

                // Push the current path onto the tokens.
                if (!string.IsNullOrEmpty(path))
                {
                    tokens.Add(new Token
                    {
                        Raw = path,
                    });
                    path = "";
                    pathEscaped = false;
                }

                var repeat = modifier == "+" || modifier == "*";
                var optional = modifier == "?" || modifier == "*";
                var pattern = !string.IsNullOrEmpty(capture) ? capture : group;
                var delimiter = !string.IsNullOrEmpty(prev) ? prev[0] : options.Delimiter;

                tokens.Add(new Token
                {
                    Index = string.IsNullOrEmpty(name) ? key++ : (int?)null,
                    Name = !string.IsNullOrEmpty(name) ? name : null,
                    Prefix = prev,
                    Delimiter = delimiter,
                    Optional = optional,
                    Repeat = repeat,
                    Pattern = !string.IsNullOrEmpty(pattern)
                        ? EscapeGroup(pattern)
                        : ("[^" + EscapeGroup(delimiter == options.Delimiter ? "" + delimiter : ("" + delimiter + options.Delimiter)) + "]+?"),
                });
            }

            // Push any remaining characters.
            if (!string.IsNullOrEmpty(path) || index < str.Length)
            {
                tokens.Add(new Token
                {
                    Raw = path + str.Substring(index),
                });
            }

            return tokens;
        }

        /// <summary>
        /// Escape the capturing group by escaping special characters and meaning.
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        private static string EscapeGroup(string group)
        {
            return Regex.Replace(group, "([=!:$/()])", "\\$1", RegexOptions.ECMAScript);
        }

        /// <summary>
        /// Escape a regular expression string.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string EscapeString(string str)
        {
            return Regex.Replace(str, "([.+*?=^!:${}()[\\]|/\\\\])", "\\$1", RegexOptions.ECMAScript);
        }

        /// <summary>
        /// Pull out keys from a regex.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public static Regex RegexToRegex(Regex path, ref List<Token> keys)
        {
            if (keys == null) return path;

            // Use a negative lookahead to match only capturing groups.
            var groups = Regex.Matches(path.ToString(), "\\((?!\\?)", RegexOptions.ECMAScript);

            if (groups.Count > 0)
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    keys.Add(new Token
                    {
                        Index = i,
                        Prefix = null,
                        Delimiter = null,
                        Optional = false,
                        Repeat = false,
                        Pattern = null,
                    });
                }
            }

            return path;
        }

        /// <summary>
        /// Expose a function for taking tokens and returning a Regex.
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="keys"></param>
        /// <param name="sensitive"></param>
        /// <param name="strict"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="delimiter"></param>
        /// <param name="endsWithChars"></param>
        /// <returns></returns>
        public static Regex TokensToRegex(List<Token> tokens, ref List<Token> keys, PathToRegexOptions options = null)
        {
            options = options ?? new PathToRegexOptions();

            var endsWith = string.Join("|",
                (options.EndsWith == null ? new char[] { } : options.EndsWith)
                    .Select(x => EscapeString("" + x))
                    .Concat(new string[] { "$" }));

            var route = options.Start ? "^" : "";

            // Iterate over the tokens and create our regex string.
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.Raw != null)
                {
                    route += EscapeString(token.Raw);
                }
                else
                {
                    var capture = token.Repeat
                      ? "(?:" + token.Pattern + ")(?:" + EscapeString(token.Delimiter?.ToString()) + "(?:" + token.Pattern + "))*"
                      : token.Pattern;

                    if (keys != null)
                        keys.Add(token);

                    if (token.Optional)
                    {
                        if (token.Prefix == null)
                        {
                            route += "(" + capture + ")?";
                        }
                        else
                        {
                            route += "(?:" + EscapeString(token.Prefix) + "(" + capture + "))?";
                        }
                    }
                    else
                    {
                        route += EscapeString(token.Prefix) + "(" + capture + ")";
                    }
                }
            }

            if (options.End)
            {
                if (!options.Strict)
                    route += "(?:" + EscapeString("" + options.Delimiter) + ")?";
                route += endsWith == "$" ? "$" : "(?=" + endsWith + ")";
            }
            else
            {
                var endToken = tokens[tokens.Count - 1];
                var isEndDelimited = endToken.Raw != null
                  ? endToken.Raw.EndsWith("" + options.Delimiter)
                  : endToken == null;

                if (!options.Strict) route += "(?:" + EscapeString("" + options.Delimiter) + "(?=" + endsWith + "))?";
                if (!isEndDelimited) route += "(?=" + EscapeString("" + options.Delimiter) + "|" + endsWith + ")";
            }

            return new Regex(route, RegexOptions.ECMAScript | (options.Sensitive ? RegexOptions.IgnoreCase : RegexOptions.None));
        }

        /// <summary>
        /// Create a path regex from string input.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="keys"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Regex StringToRegex(string path, ref List<Token> keys, PathToRegexOptions options = null)
        {
            return TokensToRegex(Parse(path, options), ref keys, options);
        }

        /// <summary>
        /// Transform an array into a regex.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="keys"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Regex ArrayToRegex(IEnumerable<string> path, ref List<Token> keys, PathToRegexOptions options = null)
        {
            var parts = new List<string>();

            var i = 0;
            foreach (var part in path)
            {
                parts.Add(PathToRegex(part, ref keys, options).ToString());
                i++;
            }

            return new Regex("(?:" + string.Join("|", parts) + ")",
                RegexOptions.ECMAScript | (options.Sensitive ? RegexOptions.IgnoreCase : RegexOptions.None));
        }

        /// <summary>
        /// Normalize the given path string, returning a regular expression.
        /// 
        /// An empty array can be passed in for the keys, which will hold the
        /// placeholder key descriptions. For example, using `/user/:id`, `keys` will
        /// contain `[{ name: 'id', delimiter: '/', optional: false, repeat: false }]`.
        /// </summary>
        /// <param name="path">string, Regex, or array of strings</param>
        /// <param name="keys"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Regex PathToRegex(object path, ref List<Token> keys, PathToRegexOptions options = null)
        {
            if (path is Regex)
            {
                return RegexToRegex((Regex)path, ref keys);
            }

            if (path is IEnumerable<string>)
            {
                return ArrayToRegex((IEnumerable<string>)path, ref keys, options);
            }

            return StringToRegex((string)path, ref keys, options);
        }
    }
}
