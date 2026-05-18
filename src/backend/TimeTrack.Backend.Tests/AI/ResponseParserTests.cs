using FluentAssertions;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.AI.Services;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class ResponseParserTests
{
    private readonly AppClassificationRequest _defaultRequest = new()
    {
        ExeName = "chrome",
        SampleWindowTitles = ["Gmail - Inbox"],
        AvgDailySeconds = 3600,
    };

    [Fact]
    public void ParseClassification_ValidJson_ReturnsClassification()
    {
        var raw = @"{""category"": ""productive"", ""subcategory"": ""communication"",
                     ""confidence"": 0.9, ""reasoning"": ""Email client""}";

        var result = ResponseParser.ParseClassification(raw, _defaultRequest);

        result.Category.Should().Be("productive");
        result.Subcategory.Should().Be("communication");
        result.Confidence.Should().BeApproximately(0.9, 0.001);
        result.Reasoning.Should().Be("Email client");
    }

    [Fact]
    public void ParseClassification_JsonInMarkdownBlock_ReturnsClassification()
    {
        var raw = "```json\n{\"category\":\"distracting\",\"subcategory\":\"social_media\",\"confidence\":0.85,\"reasoning\":\"Social\"}\n```";

        var result = ResponseParser.ParseClassification(raw, _defaultRequest);

        result.Category.Should().Be("distracting");
        result.Subcategory.Should().Be("social_media");
    }

    [Fact]
    public void ParseClassification_InvalidJson_ReturnsFallback()
    {
        var raw = "This is not JSON at all";

        var result = ResponseParser.ParseClassification(raw, _defaultRequest);

        result.Category.Should().Be("neutral");
        result.Subcategory.Should().Be("unclassified");
        result.Confidence.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void ParseClassification_EmptyString_ReturnsFallback()
    {
        var result = ResponseParser.ParseClassification("", _defaultRequest);

        result.Category.Should().Be("neutral");
        result.Confidence.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void ExtractJson_PlainJson_ReturnsAsIs()
    {
        var json = @"{""key"": ""value""}";

        var result = ResponseParser.ExtractJson(json);

        result.Should().Be(@"{""key"": ""value""}");
    }

    [Fact]
    public void ExtractJson_JsonInCodeBlock_StripsMarkdown()
    {
        var json = "```\n{\"key\": \"value\"}\n```";

        var result = ResponseParser.ExtractJson(json);

        result.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public void ExtractJson_JsonInJsonCodeBlock_StripsMarkdown()
    {
        var json = "```json\n{\"key\": \"value\"}\n```";

        var result = ResponseParser.ExtractJson(json);

        result.Should().Be("{\"key\": \"value\"}");
    }

    [Theory]
    [InlineData("weekly_narrative", "Resumo semanal indisponivel no momento.")]
    [InlineData("pattern_detected", "Padrao detectado. Descricao detalhada indisponivel.")]
    [InlineData("alert_generated", "Alerta gerado. Detalhes indisponiveis.")]
    [InlineData("reports_suggestion", "Sugestao indisponivel no momento.")]
    [InlineData("unknown_type", "Resposta nao disponivel.")]
    public void GetFallback_KnownTypes_ReturnsExpectedMessage(string decisionType, string expected)
    {
        ResponseParser.GetFallback(decisionType).Should().Be(expected);
    }

    [Fact]
    public void EscapeForJson_EscapesSpecialCharacters()
    {
        var input = "Line1\nLine2\tTab\"Quote\\Backslash\rCR";

        var result = ResponseParser.EscapeForJson(input);

        result.Should().Be("Line1\\nLine2\\tTab\\\"Quote\\\\Backslash\\rCR");
    }

    [Fact]
    public void EscapeForJson_NoSpecialChars_ReturnsSame()
    {
        var input = "Hello World";

        ResponseParser.EscapeForJson(input).Should().Be("Hello World");
    }
}
