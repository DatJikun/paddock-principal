using Microsoft.Data.Sqlite;
using Paddock.Application.Sponsors;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Sponsors;

namespace Paddock.Tests.Persistence;

/// <summary>V029 adds the terms of sponsor deals (#268) without losing a row, and a world with terms survives a save.</summary>
public sealed class SponsorTermsMigrationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v29-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AV28SaveKeepsItsSponsorRowsAsOneYearStandardDealsAndAcceptsLongerOnes()
    {
        var path = Path.Combine(_directory, "v28.paddock");
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(28)]))
        {
            Exec(created, """
                INSERT INTO sponsor_state (id, next_number) VALUES (1, 4);
                INSERT INTO sponsor_talks (number, organization_id, sponsor_id, slot, kind, manager_id, opened, status, closed_on, rival, cap_milli, full_annual_cents)
                VALUES (1, 'alfa', 'vestoil_works', 1, 'technical', 'human:anna', '1955-01-01', 'Open', NULL, 'Absent', 1050, 480000);
                INSERT INTO sponsor_deals (number, organization_id, sponsor_id, slot, kind, start_on, end_on, annual_cents, instalments_paid, objective_id, outcome, bonus_cents, status, ended_on)
                VALUES (2, 'alfa', 'corvane_fuels', 2, 'technical', '1955-01-01', '1955-12-31', 336000, 12, NULL, 'None', 0, 'Active', NULL);
                INSERT INTO sponsor_offers (number, deal_number, organization_id, sponsor_id, slot, kind, annual_cents, opened, valid_until, status, closed_on)
                VALUES (3, 2, 'alfa', 'corvane_fuels', 2, 'technical', 400000, '1955-11-01', '1955-12-31', 'Open', NULL);
                """);
            Assert.Throws<SqliteException>(() => Exec(created, "UPDATE sponsor_deals SET instalments_paid = 24 WHERE number = 2"));
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.Equal(1L, Scalar(opened, "SELECT COUNT(*) FROM sponsor_talks WHERE sponsor_id = 'vestoil_works' AND years = 1 AND ambition = 'Standard'"));
        Assert.Equal(1L, Scalar(opened, "SELECT COUNT(*) FROM sponsor_deals WHERE sponsor_id = 'corvane_fuels' AND years = 1 AND ambition = 'Standard' AND wish_nationality IS NULL AND instalments_paid = 12"));
        Assert.Equal(1L, Scalar(opened, "SELECT COUNT(*) FROM sponsor_offers WHERE number = 3 AND years = 1 AND ambition = 'Standard' AND rounds = 0"));

        Exec(opened, "UPDATE sponsor_deals SET years = 3, instalments_paid = 36 WHERE number = 2");
        Assert.Equal(36L, Scalar(opened, "SELECT instalments_paid FROM sponsor_deals WHERE number = 2"));
        Assert.Throws<SqliteException>(() => Exec(opened, "UPDATE sponsor_deals SET years = 4 WHERE number = 2"));
        Assert.Throws<SqliteException>(() => Exec(opened, "UPDATE sponsor_deals SET ambition = 'Easy' WHERE number = 2"));
        Assert.Throws<SqliteException>(() => Exec(opened, "UPDATE sponsor_deals SET instalments_paid = 37 WHERE number = 2"));
    }

    [Fact]
    public void AWorldWithTermsWishesAndCounteredOffersRoundTripsWithTheSameHash()
    {
        var kit = new SponsorKit(new GameDate(1955, 1, 1));
        kit.AddDriver("FRA", SeatStatus.Reserve);
        kit.SignDeal("club_verdane", 1, new GameDate(1955, 1, 1), SponsorTerms.Default);
        kit.SignDeal("vestoil_works", 2, new GameDate(1955, 1, 1), new SponsorTerms(3, SponsorAmbition.Harder));
        kit.SignDeal("rheinwerk_motoren", 3, new GameDate(1955, 1, 1));
        kit.Live(new GameDate(1955, 1, 1), 310);
        var offer = kit.Book.Section.OpenOffersOf(SponsorKit.Alfa).First();
        Assert.Null(kit.Run(new CounterSponsorOfferCommand
        {
            ManagerId = SponsorKit.Anna,
            IssuedOn = SponsorKit.Day(new GameDate(1955, 11, 10)),
            OrganizationId = SponsorKit.Alfa.Value,
            OfferId = offer.Id,
            Years = 2,
            Ambition = SponsorAmbition.Standard,
        }));
        var world = kit.World.WithDate(new GameDate(1955, 11, 11));

        using var file = SaveFile.Create(Path.Combine(_directory, "terms.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, new GameDate(1955, 11, 11));
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var before = kit.Book.Section;
        var after = loaded.Section<SponsorsSection>(SponsorsSection.SectionName)!;
        Assert.Equal(before.Deals, after.Deals);
        Assert.Equal(before.Offers, after.Offers);
        Assert.Equal(before.Talks, after.Talks);
        Assert.Contains(after.Deals, deal => deal.Years == 3 && deal.Ambition == SponsorAmbition.Harder);
        Assert.Contains(after.Deals, deal => deal.WishNationality == "FRA");
        Assert.Contains(after.Offers, item => item.Rounds == 1 && item.Years == 2);
    }

    private static void Exec(SaveFile file, string sql)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object Scalar(SaveFile file, string sql)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }
}
