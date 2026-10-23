// Copyright 2026 Jeff Ward, Beneficial Strategies. Usage subject to license of enclosing library.

namespace BeneficialStrategies.Iso20022.Validation.Xml.Payloads.Tests;

// The rules that run after a payload is read. They are a few of the real message's rules, written here so that this package is
// tested without the validators of the message library: what is tested is the reading, the routing and the reporting, not the rules.

/// <summary>A group header needs its message identification.</summary>
internal sealed class GroupHeaderRules : AbstractValidator<IGroupHeader104>
{
    public GroupHeaderRules()
    {
        RuleFor(x => x.MessageIdentification.Value).NotEmpty().WithName("MessageIdentification");
    }
}

/// <summary>A settlement request message needs at least one request, and as many as its header says.</summary>
internal sealed class SettlementRequestRules : AbstractValidator<IMultilateralSettlementRequestV02>
{
    public SettlementRequestRules()
    {
        RuleFor(x => x.GroupHeader).SetValidator(new GroupHeaderRules());
        RuleFor(x => x.SettlementRequest).NotEmpty();
        RuleFor(x => x.SettlementRequest.Count().ToString())
            .Equal(x => x.GroupHeader.NumberOfSettlementRequests.Value)
            .WithName("SettlementRequest");
    }
}

internal static class TestValidators
{
    /// <summary>
    /// Registers the rules above for the interfaces, as the validators of the message library register theirs. With
    /// <paramref name="withRules"/> false the message has no validator, to test what the dispatcher says then.
    /// </summary>
    public static IServiceCollection AddTestValidators(this IServiceCollection services, bool withRules = true)
    {
        services.AddSingleton<IValidator<IGroupHeader104>>(new GroupHeaderRules());
        if (withRules)
            services.AddSingleton<IValidator<IMultilateralSettlementRequestV02>>(new SettlementRequestRules());
        return services;
    }
}
