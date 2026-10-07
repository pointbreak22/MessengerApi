using Application.Moderation;
using Xunit;

namespace Tests.Moderation
{
    public class ProfanityFilterTests
    {
        private readonly ProfanityFilter _filter = new();

        [Theory]
        // обычные формы
        [InlineData("иди нахуй")]
        [InlineData("ну и хуйня")]
        [InlineData("мне похую")]
        [InlineData("охуеть можно")]
        [InlineData("пиздец полный")]
        [InlineData("Блять, опять")]
        [InlineData("заебал уже")]
        [InlineData("ты долбоёб")]
        [InlineData("разъебать всё")]
        [InlineData("съебался быстро")]
        [InlineData("ебать ты")]
        [InlineData("сука")]
        [InlineData("мудак")]
        [InlineData("пидорас")]
        [InlineData("херня какая-то")]
        [InlineData("жопа")]
        [InlineData("шлюха")]
        [InlineData("порно")]
        [InlineData("сиськи")]
        // маскировка
        [InlineData("хууууууй")]
        [InlineData("xyй")]
        [InlineData("пi3дец")]
        [InlineData("6лять")]
        [InlineData("бл@ть")]
        [InlineData("х у й")]
        [InlineData("х.у.й")]
        [InlineData("п-и-з-д-а")]
        [InlineData("х*й")]
        [InlineData("п**дец")]
        [InlineData("бл*ть")]
        [InlineData("бл.ть")]
        [InlineData("ПИЗДЕЦ")]
        [InlineData("пи​здец")]
        [InlineData("привет,сука")]
        // латиница и транслит
        [InlineData("fuck you")]
        [InlineData("f*ck")]
        [InlineData("fuuuuck")]
        [InlineData("sh1t")]
        [InlineData("bitch")]
        [InlineData("asshole")]
        [InlineData("blyat")]
        [InlineData("pizdec")]
        [InlineData("idi nahui")]
        [InlineData("cyka")]
        [InlineData("xep")]
        public void Detects_profanity(string text)
        {
            var result = _filter.Check(text);
            Assert.False(result.IsClean, $"Не поймано: \"{text}\"");
        }

        [Theory]
        [InlineData("подстрахуй меня")]
        [InlineData("застрахую машину")]
        [InlineData("не психуй")]
        [InlineData("купи скипидар")]
        [InlineData("употребляя лекарства")]
        [InlineData("не оскорбляй")]
        [InlineData("у корабля")]
        [InlineData("сто рублей, сто рубля")]
        [InlineData("наша команда")]
        [InlineData("мандарин и мандат")]
        [InlineData("свежий хлеб")]
        [InlineData("купи себе")]
        [InlineData("веб-сайт и вебинар")]
        [InlineData("голубое небо")]
        [InlineData("мудрый совет")]
        [InlineData("хулиган")]
        [InlineData("хуже некуда")]
        [InlineData("херувим")]
        [InlineData("трахея")]
        [InlineData("сравнить и сражение")]
        [InlineData("анализ и канал")]
        [InlineData("сук на дереве обломился")]
        [InlineData("бляха-муха")]
        [InlineData("*важно*")]
        [InlineData("Ху из ху")]
        [InlineData("потребляют")]
        [InlineData("истреблять")]
        [InlineData("hello world")]
        [InlineData("assistant classic pass")]
        [InlineData("Dickens and peacock")]
        [InlineData("cocktail")]
        [InlineData("ebay ebook")]
        [InlineData("Fukuoka")]
        [InlineData("shuffle")]
        [InlineData("pop copy")]
        [InlineData("Привет! Как дела? Пойдём в кино в 19:00.")]
        [InlineData("я и ты")]
        [InlineData("")]
        public void Allows_normal_text(string text)
        {
            var result = _filter.Check(text);
            Assert.True(result.IsClean, $"Ложное срабатывание: \"{text}\" → {string.Join(", ", result.MatchedWords)}");
        }

        [Theory]
        [InlineData("Блять, опять дождь", "Блин, опять дождь")]
        [InlineData("иди нахуй", "иди нафиг")]
        [InlineData("ну и хуйня!", "ну и фигня!")]
        [InlineData("мне похую", "мне пофигу")]
        [InlineData("пиздец полный", "капец полный")]
        [InlineData("ты меня заебал", "ты меня достал")]
        public void Suggests_softer_replacement(string text, string expected)
        {
            var result = _filter.Check(text);
            Assert.False(result.IsClean);
            Assert.Equal(expected, result.Suggestion);
        }

        [Fact]
        public void Reports_matched_words_as_written()
        {
            var result = _filter.Check("ну ты и Мудак, честно");
            Assert.Equal(new[] { "Мудак" }, result.MatchedWords);
        }
    }
}
