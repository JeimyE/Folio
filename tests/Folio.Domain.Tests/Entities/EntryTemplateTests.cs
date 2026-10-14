using Folio.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class EntryTemplateTests
{
    private static readonly DateOnly Date = new(2026, 3, 15);
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Constructor_BlankName_Throws()
    {
        var act = () => new EntryTemplate("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraftEntry_LinesWithDefaultAmounts_AreAddedToEntry()
    {
        var rentAccount = Guid.NewGuid();
        var bankAccount = Guid.NewGuid();
        var template = new EntryTemplate("Alquiler mensual");
        template.AddLine(new EntryTemplateLine(rentAccount, isDebit: true, defaultAmount: 300_000m));
        template.AddLine(new EntryTemplateLine(bankAccount, isDebit: false, defaultAmount: 300_000m));

        var entry = template.CreateDraftEntry(Date, UserId, Now);

        entry.Lines.Should().HaveCount(2);
        entry.Lines[0].Debit.Should().Be(300_000m);
        entry.Lines[1].Credit.Should().Be(300_000m);
    }

    [Fact]
    public void CreateDraftEntry_LineWithoutDefaultAmount_IsNotAddedToEntry()
    {
        var template = new EntryTemplate("Planilla de baristas y panaderos");
        template.AddLine(new EntryTemplateLine(Guid.NewGuid(), isDebit: true, defaultAmount: null));

        var entry = template.CreateDraftEntry(Date, UserId, Now);

        entry.Lines.Should().BeEmpty();
    }

    [Fact]
    public void CreateDraftEntry_NoDescriptionOverride_UsesTemplateDefaultDescription()
    {
        var template = new EntryTemplate("Alquiler mensual", defaultDescription: "Pago de alquiler del local");

        var entry = template.CreateDraftEntry(Date, UserId, Now);

        entry.Description.Should().Be("Pago de alquiler del local");
    }

    [Fact]
    public void CreateDraftEntry_DescriptionOverrideProvided_UsesOverride()
    {
        var template = new EntryTemplate("Alquiler mensual", defaultDescription: "Pago de alquiler del local");

        var entry = template.CreateDraftEntry(Date, UserId, Now, description: "Alquiler marzo 2026");

        entry.Description.Should().Be("Alquiler marzo 2026");
    }

    [Fact]
    public void CreateDraftEntry_ResultingEntry_IsADraft()
    {
        var template = new EntryTemplate("Alquiler mensual");

        var entry = template.CreateDraftEntry(Date, UserId, Now);

        entry.Status.Should().Be(Folio.Domain.Enums.JournalEntryStatus.Draft);
    }
}
