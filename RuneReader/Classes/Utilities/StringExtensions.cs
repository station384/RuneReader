using System;
using System.Text.RegularExpressions;

namespace RuneReader.Classes.Utilities
{
    public static class StringExtensions
    {
        /// <summary>
        /// Extracts only the characters up to the max length specified starting at position 0
        /// </summary>
        /// <param name="input">The string to extract from</param>
        /// <param name="len">Max length to extract</param>
        /// <returns>Extracted portion of string</returns>
        /// <example> 
        /// <code>
        /// Example 1:
        /// input="123456"
        /// len="4"
        /// result = "1234"
        /// Example 2:
        /// input="12"
        /// len="4"
        /// result = "12"
        /// </code>
        /// </example>
        public static string Extract(this string input, int len)
        {
            return input[0..Math.Min(input.Length, len)];
        }


        private static readonly Regex _regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
        public static bool IsTextAllowed(string text)
        {
            return !_regex.IsMatch(text);
        }

    }
}
