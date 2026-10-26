![Beneficial Strategies](https://raw.githubusercontent.com/Beneficial-Strategies/iso-20022-validation-xml-payloads/main/.beneficial/logo.png)

# BeneficialStrategies.Iso20022.Validation.Xml.Payloads

Validate raw ISO 20022 XML or JSON payloads as they arrive from a queue, an HTTP body or a file: read, then validate; an unreadable payload comes back as ordinary FluentValidation failures.

**Validate the payload the way it arrives**: off a message queue, out of an HTTP body, from a file. The payload is read, then validated with the validator you registered for the message, and a payload that cannot be read is not an exception: its problems come back as ordinary FluentValidation failures, in the same `ValidationResult`, each with the path of what is wrong.

Two building blocks, depending on whether you know the message type up front:

* **An envelope validator**, `IValidator<XmlEnvelope<TMessage>>` or `IValidator<JsonEnvelope<TMessage>>`, when the type is known at the call site (a queue bound to one message type). It is just another `IValidator<T>`.
* **The dispatcher**, `IIso20022PayloadValidationDispatcher`, when the payload could be any of several message types and its own content says which (the namespace of the XML document, or the ISO identifier you pass for JSON).

The package needs [BeneficialStrategies.Iso20022.Primitives](https://www.nuget.org/packages/BeneficialStrategies.Iso20022.Primitives), [BeneficialStrategies.Iso20022.Validation.Xml](https://www.nuget.org/packages/BeneficialStrategies.Iso20022.Validation.Xml) and FluentValidation, and nothing else: not the message definitions, not the records, not the validators of ISO's rules. Three examples follow, each adding one thing.

## How to use

### Your own message

You do not need the ISO 20022 message library, or its validators. You need a message class of your own whose members are ISO 20022 primitive types, and a validator for it. A message is an `IOuterRecord` that carries the four constants a reader needs, the same four every record of the message library carries:

<!-- snippet: examples/01-your-own-message/Program.cs#message -->
```csharp
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
```
<!-- /snippet -->

Register your validator, then the payload validators, which add an XML and a JSON envelope validator for **every message that has a validator registered**:

<!-- snippet: examples/01-your-own-message/Program.cs#register -->
```csharp
var services = new ServiceCollection();
services.AddSingleton<IValidator<MyMessage>, MyMessageValidator>();   // yours first
services.AddIso20022PayloadValidators();                               // an XML and a JSON envelope validator for each message that has a validator
services.AddIso20022PayloadValidationDispatcher();
using var provider = services.BuildServiceProvider();
```
<!-- /snippet -->

<!-- snippet: examples/01-your-own-message/Program.cs#xml -->
```csharp
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
```
<!-- /snippet -->

Valid XML gives no failures. With the charge bearer `DEBT`, your rule speaks; with `FREE`, which is not a code, the reader does, with the path of the element:

```
SHAR: IsValid=True
DEBT: IsValid=False
  EqualValidator ChargeBearer: Charges must be shared.
FREE: IsValid=False
  Xml.BadValue Document/Party/ChargeBearer: 'FREE' is not a value of ChargeBearerType1Code. Expected one of: DEBT, CRED, SHAR, SLEV.
```

JSON is the same with `JsonEnvelope<MyMessage>`. The names are the property names in `snake_case`:

<!-- snippet: examples/01-your-own-message/Program.cs#json -->
```csharp
var jsonValidator = provider.GetRequiredService<IValidator<JsonEnvelope<MyMessage>>>();
var jsonResult = jsonValidator.Validate(new JsonEnvelope<MyMessage>("{\"name\":\"Acme Corp\",\"charge_bearer\":\"FREE\"}"));
Console.WriteLine($"json: IsValid={jsonResult.IsValid}");
foreach (var error in jsonResult.Errors)
    Console.WriteLine($"  {error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
```
<!-- /snippet -->

**When you do not know the type.** The dispatcher looks at the payload, finds your message class by its `DocumentNamespace` (XML) or `IsoIdentifier` (JSON), reads it, runs its validator and hands the message back:

<!-- snippet: examples/01-your-own-message/Program.cs#dispatch -->
```csharp
var dispatcher = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>();

var dispatched = dispatcher.ValidateXml(Xml("SHAR"));
Console.WriteLine($"{dispatched.IsValid} {dispatched.MessageType!.Name}");           // True MyMessage

var unknown = dispatcher.ValidateXml("<Document xmlns=\"urn:example:something.else\"><X/></Document>");
foreach (var error in unknown.ValidationResult.Errors)
    Console.WriteLine($"{error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
```
<!-- /snippet -->

Try the [project](https://github.com/Beneficial-Strategies/iso-20022-validation-xml-payloads/tree/main/examples/01-your-own-message) yourself.

### With the message records

You would rather not write the classes: [BeneficialStrategies.Iso20022.Messages.Records](https://www.nuget.org/packages/BeneficialStrategies.Iso20022.Messages.Records) has every ISO 20022 message as a ready-made class. Register your rules, written for the **interface** of the message (the validators of the message library are written the same way, and registered the same way), then the payload validators, naming the assembly of the records:

<!-- snippet: examples/02-with-the-message-records/Program.cs#rules -->
```csharp
public class MySettlementRules : AbstractValidator<IMultilateralSettlementRequestV02>
{
    public MySettlementRules() => RuleFor(m => m.SettlementRequest).NotEmpty();
}
```
<!-- /snippet -->

<!-- snippet: examples/02-with-the-message-records/Program.cs#register -->
```csharp
var services = new ServiceCollection();
// Your rules, written for the interface of the message: the validators of the message library are registered the same way.
services.AddSingleton<IValidator<IMultilateralSettlementRequestV02>, MySettlementRules>();
services.AddIso20022PayloadValidators(messageAssemblies: [typeof(MultilateralSettlementRequestV02).Assembly]);
services.AddIso20022PayloadValidationDispatcher();
using var provider = services.BuildServiceProvider();
```
<!-- /snippet -->

A queue consumer bound to exactly one message type validates each body as it arrives, XML or JSON:

<!-- snippet: examples/02-with-the-message-records/Program.cs#known -->
```csharp
// A consumer bound to one message type validates each body as it arrives.
var validator = provider.GetRequiredService<IValidator<XmlEnvelope<MultilateralSettlementRequestV02>>>();
var result = validator.Validate(new XmlEnvelope<MultilateralSettlementRequestV02>(body));

Console.WriteLine($"IsValid={result.IsValid}");                           // IsValid=False
foreach (var error in result.Errors)
    Console.WriteLine($"  {error.ErrorCode} {error.PropertyName}: {error.ErrorMessage}");
```
<!-- /snippet -->

A queue that carries any of several message types is read by the dispatcher, which finds the type from the payload and returns the message:

<!-- snippet: examples/02-with-the-message-records/Program.cs#dispatch -->
```csharp
// A queue that carries several message types: the dispatcher finds the type from the namespace of the body and hands the message back.
var dispatcher = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>();
var dispatched = dispatcher.ValidateXml(body);

Console.WriteLine($"{dispatched.IsValid} {dispatched.MessageType!.Name}");   // False MultilateralSettlementRequestV02
if (dispatched.Message is MultilateralSettlementRequestV02 request)
    Console.WriteLine(request.GroupHeader.MessageIdentification.Value);       // MSG-001
```
<!-- /snippet -->

For XML, **every** problem is reported, not only the first. The error code is `Xml.` + the kind (`Xml.MalformedXml`, `Xml.WrongRoot`, `Xml.RequiredMissing`, `Xml.BadValue`, `Xml.FacetViolation`, `Xml.UnexpectedElement`, ...) and `PropertyName` is the path of the XML element. Try the [project](https://github.com/Beneficial-Strategies/iso-20022-validation-xml-payloads/tree/main/examples/02-with-the-message-records) yourself.

### Without the classes: the Factory

You want the message's shape, not 3,339 implementations of it. Register a [Factory](https://www.nuget.org/packages/BeneficialStrategies.Iso20022.Factory), and the XML is read into the message's **interface**: the Factory builds, on the fly, a small class that implements it. Register the factory, then your rules for the interface, then the payload validators (which look at what is registered):

<!-- snippet: examples/03-without-the-classes/Program.cs#register -->
```csharp
var services = new ServiceCollection();
services.AddIso20022Factory(typeof(IMultilateralSettlementRequestV02));            // builds its classes at start-up
services.AddSingleton<IValidator<IMultilateralSettlementRequestV02>, MySettlementRules>();
services.AddIso20022PayloadValidators();
services.AddIso20022PayloadValidationDispatcher();
using var provider = services.BuildServiceProvider();
```
<!-- /snippet -->

<!-- snippet: examples/03-without-the-classes/Program.cs#envelope -->
```csharp
var validator = provider.GetRequiredService<IValidator<XmlEnvelope<IMultilateralSettlementRequestV02>>>();
var result = validator.Validate(new XmlEnvelope<IMultilateralSettlementRequestV02>(body));
Console.WriteLine($"IsValid={result.IsValid}");                                    // IsValid=True
```
<!-- /snippet -->

The dispatcher now resolves the interface of the document's namespace and reads into it:

<!-- snippet: examples/03-without-the-classes/Program.cs#dispatch -->
```csharp
// The dispatcher resolves the interface of the document's namespace and reads into it.
var dispatched = provider.GetRequiredService<IIso20022PayloadValidationDispatcher>().ValidateXml(body);
var request = (IMultilateralSettlementRequestV02)dispatched.Message!;
Console.WriteLine($"{dispatched.MessageType!.Name} {request.GetType().Name != "MultilateralSettlementRequestV02"}");   // IMultilateralSettlementRequestV02 True: a class the Factory wrote
```
<!-- /snippet -->

The object is a class the Factory wrote for the occasion, and a normal value: equal when its members are equal. Try the [project](https://github.com/Beneficial-Strategies/iso-20022-validation-xml-payloads/tree/main/examples/03-without-the-classes) yourself.

## More

* Documentation, release notes and issues: https://github.com/Beneficial-Strategies/iso-20022-validation-xml-payloads
* License: MIT. Status: stable.

### Good to know

* **Where the message classes are found.** The dispatcher and `ValidateJson` look a type up in the `Iso20022MessageTypeRegistry` of Primitives, which holds the message classes (`IOuterRecord` with the constants above) of the loaded assemblies that have any. Name an assembly that is not loaded yet with `AddIso20022PayloadValidators(messageAssemblies: [...])`.
* **Which validator runs.** Validators of the ISO messages are written for the message *interfaces*; an envelope validates the object the payload was read into through the validator of its interface (or the validator registered for the class itself).
* **The dispatcher's own error codes** are `UnknownMessageType` (a namespace or identifier it does not know), `NoValidatorRegistered` and, for JSON, `PayloadParseError`. The envelope validators report a JSON problem as `JsonParseError`.
* **A plain method call, no container.** To validate XML with `validator.Validate(xml, out message)`, with the message handed back, use [BeneficialStrategies.Iso20022.Validation.Xml](https://www.nuget.org/packages/BeneficialStrategies.Iso20022.Validation.Xml).
* **The rules of ISO's own messages** are in [BeneficialStrategies.Iso20022.Messages.Validation](https://github.com/Beneficial-Strategies/iso-20022-messages-validation), registered the same way (`AddIso20022Validators()`); this package does not depend on it.

<!-- end of package readme -->

## Building and contributing

```
dotnet build -c Release
dotnet test -c Release --no-build
tools/run-examples.sh              # packs the package, then builds and runs every example against it
python3 tools/sync-readme.py       # fills the code blocks above from the examples
```

The .NET Framework 4.8 tests run on Windows only. A bug report or a change starts with an issue; a bug fix names the test that failed before it. See CONTRIBUTING.md.
