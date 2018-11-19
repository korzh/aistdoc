using System;
using System.Text;
using System.Text.RegularExpressions;

namespace System {

    public static class StringExtensions {
      

        /// <summary>
        /// Combines current URI string with the another URI.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="uri">The URI.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="ArgumentNullException">
        /// baseUri
        /// or
        /// uri
        /// </exception>
        public static string CombineWithUri(this string baseUri, string uri) {
            if (baseUri == null) {
                throw new ArgumentNullException(nameof(baseUri));
            }

            if (uri == null) {
                throw new ArgumentNullException(nameof(uri));
            }

            var b1 = baseUri.EndsWith("/");
            var b2 = uri.StartsWith("/");

            if (!b1 && b2 || b1 && !b2) {
                return baseUri + uri;
            }
            else if (!b1 && !b2) {
                return baseUri + "/" + uri;
            }
            else {
                return baseUri.TrimEnd('/') + uri;
            }
        }

        public static string MakeUriFromString(this string name) {
            return Regex.Replace(name.ToLower(), "[<>.,\\s]", "-"); ;
        }

    }
}
