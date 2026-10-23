// Copyright 2026 Jeff Ward, Beneficial Strategies. Usage subject to license of enclosing library.

namespace BeneficialStrategies.Iso20022.Validation.Xml.Payloads.NetStandardTests;

/// <summary>
/// The netstandard2.0 build of the payload validators, run on .NET Framework 4.8 as well as on .NET 8 and 10, with a message of the
/// tests' own and no message record.
/// </summary>
public class PayloadNetStandardTests
{
    public interface ITinyMessage : IOuterRecord
    {
        Max35Text Identification { get; }
    }

    public sealed record TinyMessage : ITinyMessage
    {
        public const string IsoIdentifier = "tiny.001.001.01";
        public const string XmlTag = "Tiny";
        public const string DocumentNamespace = "urn:test:tiny";
        public const string DocumentElementName = "Document";

        [IsoXmlTag("Id")]
        public Max35Text Identification { get; init; }
    }

    private static InlineValidator<ITinyMessage> TinyRules()
    {
        var rules = new InlineValidator<ITinyMessage>();
        rules.RuleFor(x => x.Identification).Must(id => id.Value != "BAD").WithMessage("BAD is not allowed.");
        return rules;
    }

    private static ServiceProvider TinyServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<ITinyMessage>>(TinyRules());
        services.AddIso20022PayloadValidators(messageAssemblies: [typeof(TinyMessage).Assembly]);
        services.AddIso20022PayloadValidationDispatcher();
        return services.BuildServiceProvider();
    }

    private static string TinyXml(string id) => $"<Document xmlns=\"urn:test:tiny\"><Tiny><Id>{id}</Id></Tiny></Document>";

    [Fact]
    public void XmlPayload_OfAKnownType_IsReadAndValidated()
    {
        using var provider = TinyServices();
        var sut = provider.GetRequiredService<IValidator<XmlEnvelope<TinyMessage>>>();

        Assert.True(sut.Validate(new XmlEnvelope<TinyMessage>(TinyXml("OK"))).IsValid);
        Assert.False(sut.Validate(new XmlEnvelope<TinyMessage>(TinyXml("BAD"))).IsValid);
        Assert.False(sut.Validate(new XmlEnvelope<TinyMessage>("<not-xml")).IsValid);
    }

    [Fact]
    public void XmlPayload_OfAnUnknownType_IsDispatchedByItsNamespace()
    {
        using var provider = TinyServices();
        var dispatcher = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>();

        var good = dispatcher.ValidateXml(TinyXml("OK"));
        var bad = dispatcher.ValidateXml(TinyXml("BAD"));

        Assert.True(good.IsValid);
        Assert.Equal(typeof(TinyMessage), good.MessageType);
        Assert.False(bad.IsValid);
    }

    [Fact]
    public void JsonPayload_IsReadAndValidated()
    {
        using var provider = TinyServices();
        var sut = provider.GetRequiredService<IValidator<JsonEnvelope<TinyMessage>>>();

        string Json(string id) => Iso20022JsonSerializer.SerializeToString(new TinyMessage { Identification = id });

        Assert.True(sut.Validate(new JsonEnvelope<TinyMessage>(Json("OK"))).IsValid);
        Assert.False(sut.Validate(new JsonEnvelope<TinyMessage>(Json("BAD"))).IsValid);
        Assert.False(sut.Validate(new JsonEnvelope<TinyMessage>("{not json")).IsValid);
    }
}
