// Example 03: without the classes.
// Shows:  validating a payload that is read into the interface of the message, by an object the Factory makes: no record is read.
// Needs:  BeneficialStrategies.Iso20022.Validation.Xml.Payloads, Messages (the interfaces) and Factory.
// Prints: the result for the interface, and what the dispatcher read the payload into.
using BeneficialStrategies.Iso20022.Components;
using BeneficialStrategies.Iso20022.pacs;
using BeneficialStrategies.Iso20022.Factory;
using BeneficialStrategies.Iso20022.Messages.Samples;
using BeneficialStrategies.Iso20022.Serialization;
using BeneficialStrategies.Iso20022.Validation.Xml.Payloads;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MyCompany.Examples;

// <snippet register>
var services = new ServiceCollection();
services.AddIso20022Factory(typeof(IMultilateralSettlementRequestV02));            // builds its classes at start-up
services.AddSingleton<IValidator<IMultilateralSettlementRequestV02>, MySettlementRules>();
services.AddIso20022PayloadValidators();
services.AddIso20022PayloadValidationDispatcher();
using var provider = services.BuildServiceProvider();
// </snippet>

// A body to validate: a sample of the message, written as XML.
var body = Iso20022XmlSerializer.SerializeToString(IsoSample.Minimal<IMultilateralSettlementRequestV02>());

// <snippet envelope>
var validator = provider.GetRequiredService<IValidator<XmlEnvelope<IMultilateralSettlementRequestV02>>>();
var result = validator.Validate(new XmlEnvelope<IMultilateralSettlementRequestV02>(body));
Console.WriteLine($"IsValid={result.IsValid}");                                    // IsValid=True
// </snippet>

// <snippet dispatch>
// The dispatcher resolves the interface of the document's namespace and reads into it.
var dispatched = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>().ValidateXml(body);
var request = (IMultilateralSettlementRequestV02)dispatched.Message!;
Console.WriteLine($"{dispatched.MessageType!.Name} {request.GetType().Name != "MultilateralSettlementRequestV02"}");   // IMultilateralSettlementRequestV02 True: a class the Factory wrote
// </snippet>

namespace MyCompany.Examples
{
    public class MySettlementRules : AbstractValidator<IMultilateralSettlementRequestV02>
    {
        public MySettlementRules() => RuleFor(m => m.SettlementRequest).NotEmpty();
    }
}
