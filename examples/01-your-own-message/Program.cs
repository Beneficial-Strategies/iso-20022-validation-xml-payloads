// Example 01: your own message.
// Shows:  validating raw XML and JSON payloads of a message class of your own, and dispatching a payload whose type is not known up front.
// Needs:  only BeneficialStrategies.Iso20022.Validation.Xml.Payloads (which brings Primitives, Validation.Xml and FluentValidation).
// Prints: the failures of an XML payload with a rule broken, one with a value that is not a code, a JSON payload, and the dispatcher's result.
using BeneficialStrategies.Iso20022.Codesets;
using BeneficialStrategies.Iso20022.Framework;
using BeneficialStrategies.Iso20022.SimpleTypes;
using BeneficialStrategies.Iso20022.Validation.Xml.Payloads;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Examples;

// <snippet register>
var services = new ServiceCollection();
services.AddSingleton<IValidator<MyMessage>, MyMessageValidator>();   // yours first
services.AddIso20022PayloadValidators();                               // an XML and a JSON envelope validator for each message that has a validator
services.AddIso20022PayloadValidationDispatcher();
using var provider = services.BuildServiceProvider();
// </snippet>

// <snippet xml>
var validator = provider.GetRequiredService<IValidator<XmlEnvelope<MyMessage>>>();

string Xml(string chargeBearer) =>
    $"<Document xmlns=\"urn:example:xmpl.001.001.01\"><Party><Name>Acme Corp</Name><ChargeBearer>{chargeBearer}</ChargeBearer></Party></Document>";

foreach (var chargeBearer in new[] { "SHAR", "DEBT", "FREE" })
{
    var result = validator.Validate(new XmlEnvelope<MyMessage>(Xml(chargeBearer)));
    Console.WriteLine($"{chargeBearer}: IsValid={result.IsValid}");
    foreach (var error in result.Errors)
        Console.WriteLine($"  {error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
}
// </snippet>

// <snippet json>
var jsonValidator = provider.GetRequiredService<IValidator<JsonEnvelope<MyMessage>>>();
var jsonResult = jsonValidator.Validate(new JsonEnvelope<MyMessage>("{\"name\":\"Acme Corp\",\"charge_bearer\":\"FREE\"}"));
Console.WriteLine($"json: IsValid={jsonResult.IsValid}");
foreach (var error in jsonResult.Errors)
    Console.WriteLine($"  {error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
// </snippet>

// <snippet dispatch>
var dispatcher = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>();

var dispatched = dispatcher.ValidateXml(Xml("SHAR"));
Console.WriteLine($"{dispatched.IsValid} {dispatched.MessageType!.Name}");           // True MyMessage

var unknown = dispatcher.ValidateXml("<Document xmlns=\"urn:example:something.else\"><X/></Document>");
foreach (var error in unknown.ValidationResult.Errors)
    Console.WriteLine($"{error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
// </snippet>

namespace MyCompany.Examples
{
    // <snippet message>
    // A message is an IOuterRecord that carries the four constants a reader needs, the same four every record of the message library carries.
    public class MyMessage : IOuterRecord
    {
        public const string IsoIdentifier = "xmpl.001.001.01";            // the ISO message identifier
        public const string XmlTag = "Party";                             // the element that holds the message in the document
        public const string DocumentNamespace = "urn:example:xmpl.001.001.01";
        public const string DocumentElementName = "Document";

        [IsoXmlTag("Name")]
        public required Max35Text Name { get; init; }                     // a primitive type: at most 35 characters

        [IsoXmlTag("ChargeBearer")]
        public required ChargeBearerType1Code ChargeBearer { get; init; } // a code set: DEBT, CRED, SHAR or SLEV
    }

    // An ordinary FluentValidation validator, with the rules of your own. The rules of the types are enforced as the payload is read.
    public class MyMessageValidator : AbstractValidator<MyMessage>
    {
        public MyMessageValidator() =>
            RuleFor(m => m.ChargeBearer).Equal(ChargeBearerType1Code.Shared).WithMessage("Charges must be shared.");
    }
    // </snippet>
}
