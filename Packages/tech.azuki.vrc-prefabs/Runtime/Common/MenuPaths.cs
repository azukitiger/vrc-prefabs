using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VAP {
    public static class MenuPaths {
        public static string[] Split(string path) =>
            (path ?? "").Split('/').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

        public static string ValidChars(string path) {
            var text = new StringBuilder();
            foreach (char ch in path ?? "") {
                if (!char.IsControl(ch) && ch != '\\') {
                    text.Append(ch);
                }
            }

            return text.ToString();
        }

        public static string Clean(string path) => string.Join("/", Split(ValidChars(path)));

        public static string Folder(string path) {
            var parts = Split(path);
            return string.Join("/", parts.Take(parts.Length - 1));
        }

        public static string Common(IEnumerable<string> paths) {
            List<string> common = null;
            foreach (var path in paths) {
                var parts = Split(path);
                if (common == null) {
                    common = parts.ToList();
                } else {
                    int n = 0;
                    while (n < common.Count && n < parts.Length && common[n] == parts[n]) {
                        n++;
                    }

                    common.RemoveRange(n, common.Count - n);
                }
                if (common.Count == 0) {
                    return "";
                }
            }
            return common == null ? "" : string.Join("/", common);
        }

        public static string ApplyFolder(string path, string replacement) {
            replacement = Clean(replacement);
            var parts = Split(path);
            if (replacement.Length == 0 || parts.Length == 0) {
                return path;
            }

            return replacement + "/" + parts[parts.Length - 1];
        }

        public static string Apply(string path, string common, string replacement) {
            replacement = Clean(replacement);
            if (replacement.Length == 0 || string.IsNullOrEmpty(common)) {
                return path;
            }

            var parts = Split(path);
            int n = Split(common).Length;
            if (parts.Length < n || !parts.Take(n).SequenceEqual(Split(common))) {
                return path;
            }

            return string.Join("/", Split(replacement).Concat(parts.Skip(n)));
        }
    }
}
