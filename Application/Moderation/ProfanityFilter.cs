using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.Moderation
{
    /// <summary>
    /// Ищет мат и "ниже пояса" в нике, названии чата или сообщении, в том числе
    /// замаскированный:
    ///  - латиница и цифры вместо похожих букв ("xyй", "6лять", "п1зда", "@");
    ///  - растянутые буквы ("хуууууй"), ё/е, мягкий знак;
    ///  - буквы через пробел, точку, дефис ("х у й", "х.у.й", "п-и-з-д-а");
    ///  - звёздочки вместо букв ("х*й", "п**дец", "f*ck");
    ///  - невидимые символы и диакритика поверх букв.
    /// Нормальные слова с "плохой" подстрокой ("подстрахуй", "скипидар", "употребляя",
    /// "команда", "хлеб") не трогает: корни привязаны к началу слова/приставкам,
    /// а остальное отсекается списком исключений (см. ProfanityDictionary).
    ///
    /// Без состояния и потокобезопасен — регистрируется синглтоном.
    /// </summary>
    public sealed class ProfanityFilter : IContentFilter
    {
        private const string CyrillicAlphabet = "абвгдежзийклмнопрстуфхцчшщъыэюя";
        private const string LatinAlphabet = "abcdefghijklmnopqrstuvwxyz";

        // Латинские буквы, которые выглядят как русские. Для слов, где уже есть
        // кириллица ("xyй", "пиzдец"), берём широкий набор; чисто латинское слово
        // читаем как русское, только если все его буквы из узкого набора
        // однозначных двойников ("xep", "cyka") — иначе "pop" станет "рор".
        private static readonly Dictionary<char, char> MixedLookalikes = new()
        {
            ['a'] = 'а', ['b'] = 'в', ['c'] = 'с', ['d'] = 'д', ['e'] = 'е', ['h'] = 'н', ['i'] = 'и',
            ['k'] = 'к', ['m'] = 'м', ['n'] = 'п', ['o'] = 'о', ['p'] = 'р', ['r'] = 'г', ['t'] = 'т',
            ['u'] = 'и', ['x'] = 'х', ['y'] = 'у', ['z'] = 'з',
            ['0'] = 'о', ['1'] = 'и', ['3'] = 'з', ['4'] = 'ч', ['6'] = 'б', ['@'] = 'а', ['і'] = 'и', ['ї'] = 'и',
        };
        private const string StrictLookalikes = "acekmoptxy";

        private static readonly Dictionary<char, char> LatinLeet = new()
        {
            ['0'] = 'o', ['1'] = 'i', ['3'] = 'e', ['4'] = 'a', ['5'] = 's', ['7'] = 't',
            ['@'] = 'a', ['$'] = 's', ['!'] = 'i', ['|'] = 'i',
        };

        private static readonly Dictionary<string, string> Replacements =
            ProfanityDictionary.WordReplacements
                .GroupBy(kv => NormalizeWord(kv.Key))
                .ToDictionary(g => g.Key, g => g.First().Value);

        private static readonly string[] CyrillicMaskTargets = Replacements.Keys
            .Concat(ProfanityDictionary.MaskTargets.Select(NormalizeWord))
            .Where(IsCyrillicWord).Distinct().ToArray();

        private static readonly string[] LatinMaskTargets = Replacements.Keys
            .Concat(ProfanityDictionary.MaskTargets.Select(NormalizeWord))
            .Where(w => !IsCyrillicWord(w)).Distinct().ToArray();

        // Разделители, которыми разбивают слово на буквы: "х.у.й", "п-и-з-д-а".
        private static readonly Regex InnerSeparators = new("[^\\p{L}\\p{N}*@$#!|]+", RegexOptions.Compiled);

        public ContentCheckResult Check(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return ContentCheckResult.Clean;

            var chunks = SplitChunks(text);
            var bad = new bool[chunks.Count];

            for (var i = 0; i < chunks.Count; i++)
                bad[i] = IsBadChunk(chunks[i].Core);

            // "х у й", "п и з д а" — подряд идущие однобуквенные куски склеиваем.
            var runs = new List<(int From, int To)>();
            for (var i = 0; i < chunks.Count;)
            {
                if (!IsSingleLetter(chunks[i].Core)) { i++; continue; }
                var j = i;
                while (j + 1 < chunks.Count && IsSingleLetter(chunks[j + 1].Core)) j++;
                if (j - i + 1 >= 3)
                {
                    var joined = string.Concat(chunks.Skip(i).Take(j - i + 1).Select(c => c.Core));
                    if (IsBadChunk(joined)) runs.Add((i, j));
                }
                i = j + 1;
            }

            if (!bad.Any(b => b) && runs.Count == 0) return ContentCheckResult.Clean;

            var matched = new List<string>();
            var suggestion = new StringBuilder();
            var pos = 0;

            for (var i = 0; i < chunks.Count; i++)
            {
                var run = runs.FirstOrDefault(r => r.From == i);
                if (run != default)
                {
                    var first = chunks[run.From];
                    var last = chunks[run.To];
                    var original = text.Substring(first.Start, last.Start + last.Length - first.Start);
                    var joined = string.Concat(chunks.Skip(run.From).Take(run.To - run.From + 1).Select(c => c.Core));
                    matched.Add(original);
                    suggestion.Append(text, pos, first.Start - pos);
                    suggestion.Append(Replace(joined));
                    pos = last.Start + last.Length;
                    i = run.To;
                    continue;
                }

                if (!bad[i]) continue;

                var c = chunks[i];
                matched.Add(c.Core);
                var coreStart = c.Start + c.CoreOffset;
                suggestion.Append(text, pos, coreStart - pos);
                suggestion.Append(Replace(c.Core));
                pos = coreStart + c.Core.Length;
            }
            suggestion.Append(text, pos, text.Length - pos);

            var suggested = suggestion.ToString();
            // Подсказка, в которой не осталось ни одного слова или которая сама не
            // проходит проверку, бесполезна — лучше не предлагать её вовсе.
            var useful = suggested.Any(char.IsLetter) && !ContainsProfanity(suggested);
            return new ContentCheckResult(false, matched, useful ? suggested : null);
        }

        private bool ContainsProfanity(string text)
        {
            var chunks = SplitChunks(text);
            return chunks.Any(c => IsBadChunk(c.Core));
        }

        // ---------- разбор текста ----------

        private sealed record Chunk(int Start, int Length, int CoreOffset, string Core);

        // Кусок — всё между пробелами. Core — он же без обрамляющей пунктуации
        // ("блять," → "блять"), звёздочки внутри и по краям остаются: это маска.
        private static List<Chunk> SplitChunks(string text)
        {
            var result = new List<Chunk>();
            var i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                if (i == start) continue;

                var raw = text.Substring(start, i - start);
                var from = 0;
                var to = raw.Length;
                while (from < to && !IsCoreChar(raw[from])) from++;
                while (to > from && !IsCoreChar(raw[to - 1])) to--;
                if (to > from) result.Add(new Chunk(start, raw.Length, from, raw.Substring(from, to - from)));
            }
            return result;
        }

        private static bool IsCoreChar(char c) => char.IsLetterOrDigit(c) || c is '*' or '@' or '$' or '#';

        private static bool IsSingleLetter(string core) => core.Length == 1 && char.IsLetter(core[0]);

        // ---------- проверка одного куска ----------

        private static bool IsBadChunk(string core)
        {
            var prepared = Prepare(core);
            if (prepared.Length == 0) return false;

            var candidates = new List<string> { prepared };

            // "х.у.й" / "при-вет,сука" — и склеенный вариант, и части по отдельности.
            var parts = InnerSeparators.Split(prepared).Where(p => p.Length > 0).ToArray();
            if (parts.Length > 1)
            {
                candidates.Add(string.Concat(parts));
                candidates.AddRange(parts);
                // Точка/дефис внутри слова бывает и маской вместо буквы: "бл.ть", "п-здец".
                candidates.Add(InnerSeparators.Replace(prepared, "*"));
            }

            // "@" и "$" — то похожая буква ("бл@" = "бла"), то просто маска ("бл@ть" = "блять").
            if (prepared.IndexOfAny(new[] { '@', '$' }) >= 0)
                candidates.Add(prepared.Replace('@', '*').Replace('$', '*'));

            return candidates.Any(IsBadToken);
        }

        // NFKC схлопывает "широкие" и стилизованные символы (𝐡𝐮𝐢, ｈｕｉ) в обычные,
        // дальше выкидываем невидимые символы и диакритику, навешанную поверх букв.
        private static string Prepare(string s)
        {
            var normalized = s.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format or UnicodeCategory.EnclosingMark) continue;
                if (ch == '#') { sb.Append('*'); continue; }
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private static bool IsBadToken(string token)
        {
            var hasCyr = token.Any(IsCyrillic);
            var hasLat = token.Any(c => c is >= 'a' and <= 'z');

            if (hasCyr || (hasLat && token.Where(char.IsLetter).All(c => StrictLookalikes.Contains(c))))
            {
                var cyr = new string(token.Select(c => MixedLookalikes.TryGetValue(c, out var m) ? m : c)
                    .Where(c => IsCyrillic(c) || c == '*').ToArray());
                if (MatchesCyrillic(NormalizeWord(cyr))) return true;
            }

            if (hasLat && !hasCyr)
            {
                var lat = new string(token.Select(c => LatinLeet.TryGetValue(c, out var m) ? m : c)
                    .Where(c => c is >= 'a' and <= 'z' or '*').ToArray());
                if (MatchesLatin(lat)) return true;
            }

            return false;
        }

        private static bool MatchesCyrillic(string word)
        {
            if (word.Contains('*')) return MatchesMasked(word, CyrillicAlphabet, CyrillicMaskTargets, w => MatchesCyrillic(NormalizeWord(w)));
            return word.Length > 0 && ProfanityDictionary.Cyrillic.Any(p => HasRealMatch(p, word));
        }

        private static bool MatchesLatin(string word)
        {
            if (word.Contains('*')) return MatchesMasked(word, LatinAlphabet, LatinMaskTargets, MatchesLatin);
            if (word.Length == 0) return false;
            // Латиницу проверяем и как есть, и со схлопнутыми повторами: "ass" и
            // "boobs" пишутся с двойными буквами, а "fuuuuck" — растянутое "fuck".
            var collapsed = CollapseRepeats(word);
            return ProfanityDictionary.Latin.Any(p => p.IsMatch(word) || p.IsMatch(collapsed));
        }

        // Совпадение не считается, если оно начинается внутри слова-исключения:
        // "хуй" в "подстрахуй" лежит внутри "страху".
        private static bool HasRealMatch(Regex pattern, string word)
        {
            foreach (Match m in pattern.Matches(word))
            {
                var covered = false;
                foreach (var ex in ProfanityDictionary.Exceptions)
                {
                    var idx = word.IndexOf(ex, StringComparison.Ordinal);
                    while (idx >= 0)
                    {
                        if (m.Index >= idx && m.Index < idx + ex.Length) { covered = true; break; }
                        idx = word.IndexOf(ex, idx + 1, StringComparison.Ordinal);
                    }
                    if (covered) break;
                }
                if (!covered) return true;
            }
            return false;
        }

        // Слово со звёздочками: "х*й", "п**дец", "*бать", "f*ck".
        //  1) одна звёздочка — перебираем букву на её месте и прогоняем обычные шаблоны;
        //  2) любое число звёздочек — сравниваем маску с образцами плохих слов,
        //     серия из k звёздочек может прятать от 1 до k+2 букв ("п***ц" = "пиздец").
        // Нужно минимум две настоящие буквы: "х*" или "***" может быть чем угодно.
        private static bool MatchesMasked(string word, string alphabet, string[] targets, Func<string, bool> check)
        {
            var letters = word.Count(c => c != '*');
            if (letters < 2) return false;

            var starCount = word.Count(c => c == '*');
            if (starCount == 1)
            {
                var star = word.IndexOf('*');
                foreach (var ch in alphabet)
                {
                    var candidate = word.Substring(0, star) + ch + word.Substring(star + 1);
                    if (check(candidate)) return true;
                }
            }

            var pattern = new StringBuilder("^");
            for (var i = 0; i < word.Length;)
            {
                if (word[i] != '*') { pattern.Append(Regex.Escape(word[i].ToString())); i++; continue; }
                var k = 0;
                while (i < word.Length && word[i] == '*') { k++; i++; }
                pattern.Append(".{1,").Append(k + 2).Append('}');
            }
            pattern.Append('$');

            var regex = new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
            return targets.Any(regex.IsMatch);
        }

        // ---------- подсказка-замена ----------

        private static string Replace(string core)
        {
            var key = NormalizeWord(LettersForKey(Prepare(core)));
            string? replacement = null;

            if (Replacements.TryGetValue(key, out var direct))
            {
                replacement = direct;
            }
            else
            {
                var softened = key;
                foreach (var (pattern, value) in ProfanityDictionary.RootReplacements)
                    softened = pattern.Replace(softened, value);
                if (softened != key && !IsBadChunk(softened)) replacement = softened;
            }

            replacement ??= new string('*', Math.Max(3, core.Length));
            return core.Length > 0 && char.IsUpper(core[0]) && replacement.Length > 0
                ? char.ToUpperInvariant(replacement[0]) + replacement.Substring(1)
                : replacement;
        }

        private static string LettersForKey(string prepared)
        {
            var hasCyr = prepared.Any(IsCyrillic);
            var mapped = hasCyr
                ? prepared.Select(c => MixedLookalikes.TryGetValue(c, out var m) ? m : c)
                : prepared.Select(c => LatinLeet.TryGetValue(c, out var m) ? m : c);
            return new string(mapped.Where(char.IsLetter).ToArray());
        }

        // ---------- нормализация ----------

        /// <summary>
        /// Нижний регистр, ё→е, без ь, повторы букв схлопнуты. Латиницу только
        /// приводит к нижнему регистру — для неё повторы проверяются отдельно.
        /// </summary>
        internal static string NormalizeWord(string word)
        {
            var lower = word.ToLowerInvariant();
            if (!lower.Any(IsCyrillic)) return lower;

            var sb = new StringBuilder(lower.Length);
            foreach (var raw in lower)
            {
                var c = raw == 'ё' ? 'е' : raw;
                if (c == 'ь') continue;
                if (sb.Length > 0 && sb[^1] == c && c != '*') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string CollapseRepeats(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (var c in word)
                if (sb.Length == 0 || sb[^1] != c) sb.Append(c);
            return sb.ToString();
        }

        private static bool IsCyrillic(char c) => c is >= 'а' and <= 'я' or 'ё' or 'і' or 'ї';

        private static bool IsCyrillicWord(string w) => w.Any(IsCyrillic);
    }
}
