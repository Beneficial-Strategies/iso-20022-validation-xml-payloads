// Example 02: with the message records.
// Shows:  a queue consumer that validates the body of a message of the message library, and the dispatcher for a body of any type.
// Needs:  BeneficialStrategies.Iso20022.Validation.Xml.Payloads and BeneficialStrategies.Iso20022.Messages.Records.
// Prints: the result of a good body, and of one that breaks a rule, by the type and by the dispatcher.
using BeneficialStrategies.Iso20022.Components;
using BeneficialStrategies.Iso20022.pacs;
using BeneficialStrategies.Iso20022.Serialization;
using BeneficialStrategies.Iso20022.Validation.Xml.Payloads;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Examples;

// <snippet register>
var services = new ServiceCollection();
// Your rules, written for the interface of the message: the validators of the message library are registered the same way.
services.AddSingleton<IValidator<IMultilateralSettlementRequestV02>, MySettlementRules>();
services.AddIso20022PayloadValidators(messageAssemblies: [typeof(MultilateralSettlementRequestV02).Assembly]);
services.AddIso20022PayloadValidationDispatcher();
using var provider = services.BuildServiceProvider();
// </snippet>

var message = new MultilateralSettlementRequestV02
{
    GroupHeader = new GroupHeader104
    {
        MessageIdentification = "MSG-001",
        CreationDateTime = new DateTime(2026, 1, 1),
        NumberOfSettlementRequests = "1",
    },
    SettlementRequest = [],
};
var body = Iso20022XmlSerializer.SerializeToString(message);

// <snippet known>
// A consumer bound to one message type validates each body as it arrives.
var validator = provider.GetRequiredService<IValidator<XmlEnvelope<MultilateralSettlementRequestV02>>>();
var result = validator.Validate(new XmlEnvelope<MultilateralSettlementRequestV02>(body));

Console.WriteLine($"IsValid={result.IsValid}");                           // IsValid=False
foreach (var error in result.Errors)
    Console.WriteLine($"  {error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
// </snippet>

// <snippet dispatch>
// A queue that carries several message types: the dispatcher finds the type from the namespace of the body and hands the message back.
var dispatcher = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>();
var dispatched = dispatcher.ValidateXml(body);

Console.WriteLine($"{dispatched.IsValid} {dispatched.MessageType!.Name}");   // False MultilateralSettlementRequestV02
if (dispatched.Message is MultilateralSettlementRequestV02 request)
    Console.WriteLine(request.GroupHeader.MessageIdentification.Value);       // MSG-001
// </snippet>

namespace MyCompany.Examples
{
    // <snippet rules>
    public class MySettlementRules : AbstractValidator<IMultilateralSettlementRequestV02>
    {
        public MySettlementRules() => RuleFor(m => m.SettlementRequest).NotEmpty();
    }
    // </snippet>
}
