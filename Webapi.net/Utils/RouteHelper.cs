using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;

namespace Webapi.net
{
    public static class RouteHelper
    {
        /// <summary>
        ///  Backbone style routes, most credit to Backbone authors.
        /// </summary>
        static Regex optionalParam = new Regex(@"\((.*?)\)", RegexOptions.ECMAScript | RegexOptions.Compiled);
        static Regex namedFinalParam = new Regex(@"(\(\?)?::(\w+)", RegexOptions.ECMAScript | RegexOptions.Compiled);
        static Regex namedParam = new Regex(@"(\(\?)?:\w+", RegexOptions.ECMAScript | RegexOptions.Compiled);
        static Regex namedNumericParam = new Regex(@"(\(\?)?#\w+", RegexOptions.ECMAScript | RegexOptions.Compiled);
        static Regex splatParam = new Regex(@"\*\w+", RegexOptions.ECMAScript | RegexOptions.Compiled);
        static Regex escapeRegExp = new Regex(@"[\-{}\[\]+?.,\\\^$|\s]", RegexOptions.ECMAScript | RegexOptions.Compiled);

        public static Regex RouteToRegex(string route)
        {
            if (route.StartsWith(@"/")) route = route.Remove(0, 1);

            route = splatParam.Replace(
                namedParam.Replace(
                namedFinalParam.Replace(
                namedNumericParam.Replace(
                optionalParam.Replace(
                escapeRegExp.Replace(route, @"\$&"), // Escape regex while leaving our special characters intact
                @"(?:$1)?"), // optionalParam
                m => m.Groups[1].Captures.Count == 1 ? m.Value : @"([0-9]+)"), // namedNumericParam
                m => m.Groups[1].Captures.Count == 1 ? m.Value : (@"(" + m.Groups[2].Captures[0].Value + ")")), // namedFinalParam
                m => m.Groups[1].Captures.Count == 1 ? m.Value : @"([^/]+)"), // namedParam
                @"([^?]*?)"); // splatParam
            return new Regex('^' + route + @"/?$", RegexOptions.ECMAScript | RegexOptions.Compiled);
        }
    }
}
