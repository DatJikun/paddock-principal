using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Paddock.Domain.Time;

namespace Paddock.Domain.World;

public sealed partial class WorldState
{
    /// <summary>
    /// SHA-256 of the canonical UTF-8 text, lowercase hex. Not <see cref="object.GetHashCode"/>.
    /// The same contents always hash the same, including across processes.
    /// </summary>
    /// <remarks>
    /// Format name: <c>paddock-world/1</c>.
    /// The digest is SHA-256 over the UTF-8 bytes of the text produced below.
    /// Each line ends with LF. A string is <c>length:text</c>, where length is <see cref="string.Length"/>
    /// (UTF-16 code units). Stored strings contain no control characters, so a line stays one line.
    /// Integers use the invariant culture. Dates are <c>yyyy-MM-dd</c>. Bools are <c>0</c> or <c>1</c>.
    /// A missing date or amount is the single character <c>-</c> inside a length prefix (<c>1:-</c>).
    /// Sections always appear in this order. Rows inside a section follow ordinal order of the canonical id.
    /// Attributes follow ordinal order of the key. Insertion order does not change the text.
    ///
    /// <code>
    /// paddock-world/1
    /// date &lt;len&gt;:&lt;yyyy-MM-dd&gt;
    /// ids person &lt;nextPerson&gt; organization &lt;nextOrganization&gt; contract &lt;nextContract&gt;
    /// issued &lt;count&gt;
    /// id &lt;len&gt;:&lt;canonical&gt;
    /// persons &lt;count&gt;
    /// person &lt;len&gt;:&lt;id&gt;
    /// real &lt;0|1&gt;
    /// given &lt;len&gt;:&lt;name&gt;
    /// family &lt;len&gt;:&lt;name&gt;
    /// born &lt;len&gt;:&lt;date&gt;
    /// nationality &lt;len&gt;:&lt;text&gt;
    /// retired &lt;len&gt;:&lt;date&gt;         (only for a retired person; an active person has no such line)
    /// roles &lt;count&gt;
    /// role &lt;len&gt;:&lt;driver or staff:RoleName&gt;
    /// attributes &lt;count&gt;
    /// attribute &lt;len&gt;:&lt;key&gt; &lt;value&gt;
    /// potential &lt;count&gt;
    /// potential &lt;len&gt;:&lt;key&gt; &lt;value&gt;
    /// organizations &lt;count&gt;
    /// organization &lt;len&gt;:&lt;id&gt;
    /// kind &lt;Team|EngineSupplier|Sponsor|SeriesBody&gt;
    /// real &lt;0|1&gt;
    /// founded &lt;len&gt;:&lt;date&gt;
    /// dissolved &lt;len&gt;:&lt;date or -&gt;
    /// budget &lt;long&gt;
    /// names &lt;count&gt;
    /// name &lt;len&gt;:&lt;from&gt; &lt;len&gt;:&lt;to or -&gt; &lt;len&gt;:&lt;text&gt;
    /// links &lt;count&gt;
    /// link &lt;predecessor|successor&gt; &lt;len&gt;:&lt;otherId&gt; &lt;len&gt;:&lt;from&gt; &lt;len&gt;:&lt;to or -&gt;
    /// contracts &lt;count&gt;
    /// contract &lt;len&gt;:&lt;id&gt;
    /// person &lt;len&gt;:&lt;personId&gt;
    /// organization &lt;len&gt;:&lt;organizationId&gt;
    /// exclusive &lt;0|1&gt;
    /// role &lt;len&gt;:&lt;driver:Seat or staff:RoleName&gt;
    /// start &lt;len&gt;:&lt;date&gt;
    /// end &lt;len&gt;:&lt;date&gt;
    /// salary &lt;long&gt;
    /// option &lt;0|1&gt;
    /// option-deadline &lt;len&gt;:&lt;date&gt;    (only when option is 1)
    /// option-years &lt;int&gt;                   (only when option is 1)
    /// release &lt;0|1&gt;
    /// release-amount &lt;long&gt;                (only when release is 1)
    /// knowledge &lt;count&gt;
    /// belief &lt;len&gt;:&lt;observerId&gt; &lt;len&gt;:&lt;subjectId&gt;
    /// bands &lt;count&gt;
    /// band &lt;len&gt;:&lt;key&gt; &lt;low&gt; &lt;high&gt;
    /// potential-band &lt;0|1&gt;
    /// potential-low &lt;int&gt;                  (only when potential-band is 1)
    /// potential-high &lt;int&gt;                 (only when potential-band is 1)
    /// </code>
    /// One changed attribute changes this digest.
    /// </remarks>
    public string StateHash()
    {
        var text = CanonicalText();
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private string CanonicalText()
    {
        var canon = new Canon();
        canon.Line("paddock-world/1");
        canon.TextLine("date", CurrentDate.ToString());
        canon.Line(
            "ids person "
            + Ids.NextPerson.ToString(CultureInfo.InvariantCulture)
            + " organization "
            + Ids.NextOrganization.ToString(CultureInfo.InvariantCulture)
            + " contract "
            + Ids.NextContract.ToString(CultureInfo.InvariantCulture));
        canon.Count("issued", Ids.Issued.Count);
        foreach (var id in Ids.Issued)
        {
            canon.TextLine("id", id);
        }

        var persons = Persons;
        canon.Count("persons", persons.Count);
        foreach (var person in persons)
        {
            canon.TextLine("person", person.Id.Value);
            canon.Flag("real", person.IsReal);
            canon.TextLine("given", person.GivenName);
            canon.TextLine("family", person.FamilyName);
            canon.TextLine("born", person.BirthDate.ToString());
            canon.TextLine("nationality", person.Nationality);
            if (person.RetiredOn is GameDate retiredOn)
            {
                canon.TextLine("retired", retiredOn.ToString());
            }

            canon.Count("roles", person.Roles.Count);
            foreach (var role in person.Roles)
            {
                canon.TextLine("role", role.ToString());
            }

            canon.Count("attributes", person.Truth.Attributes.Count);
            foreach (var attribute in person.Truth.Attributes)
            {
                canon.TextNumber("attribute", attribute.Key, attribute.Value);
            }

            canon.Count("potential", person.Truth.Potential.Count);
            foreach (var attribute in person.Truth.Potential)
            {
                canon.TextNumber("potential", attribute.Key, attribute.Value);
            }
        }

        var organizations = Organizations;
        canon.Count("organizations", organizations.Count);
        foreach (var organization in organizations)
        {
            canon.TextLine("organization", organization.Id.Value);
            canon.Line("kind " + organization.Kind.ToString());
            canon.Flag("real", organization.IsReal);
            canon.TextLine("founded", organization.Founded.ToString());
            canon.TextLine("dissolved", organization.Dissolved?.ToString() ?? "-");
            canon.Number("budget", organization.Budget);
            canon.Count("names", organization.Names.Count);
            foreach (var span in organization.Names)
            {
                canon.Begin("name");
                canon.Field(span.From.ToString());
                canon.Space();
                canon.Field(span.To?.ToString() ?? "-");
                canon.Space();
                canon.Field(span.Name);
                canon.End();
            }

            canon.Count("links", organization.Lineage.Count);
            foreach (var link in organization.Lineage)
            {
                canon.Begin("link " + (link.Direction == LineageDirection.Predecessor ? "predecessor" : "successor"));
                canon.Field(link.OtherId.Value);
                canon.Space();
                canon.Field(link.From.ToString());
                canon.Space();
                canon.Field(link.To?.ToString() ?? "-");
                canon.End();
            }
        }

        var contracts = Contracts;
        canon.Count("contracts", contracts.Count);
        foreach (var contract in contracts)
        {
            canon.TextLine("contract", contract.Id.Value);
            canon.TextLine("person", contract.PersonId.Value);
            canon.TextLine("organization", contract.OrganizationId.Value);
            canon.Flag("exclusive", contract.Exclusive);
            canon.TextLine("role", contract.Role.ToString());
            canon.TextLine("start", contract.Start.ToString());
            canon.TextLine("end", contract.End.ToString());
            canon.Number("salary", contract.Salary);
            canon.Flag("option", contract.Option is not null);
            if (contract.Option is ContractOption option)
            {
                canon.TextLine("option-deadline", option.Deadline.ToString());
                canon.Number("option-years", option.ExtraYears);
            }

            canon.Flag("release", contract.ReleaseClause is not null);
            if (contract.ReleaseClause is ReleaseClause release)
            {
                canon.Number("release-amount", release.Amount);
            }
        }

        var knowledge = Knowledge;
        canon.Count("knowledge", knowledge.Count);
        foreach (var belief in knowledge)
        {
            canon.Begin("belief");
            canon.Field(belief.ObserverId.Value);
            canon.Space();
            canon.Field(belief.SubjectId.Value);
            canon.End();
            canon.Count("bands", belief.Attributes.Count);
            foreach (var attribute in belief.Attributes)
            {
                canon.Begin("band");
                canon.Field(attribute.Key);
                canon.Space();
                canon.Raw(attribute.Band.Low.ToString(CultureInfo.InvariantCulture));
                canon.Space();
                canon.Raw(attribute.Band.High.ToString(CultureInfo.InvariantCulture));
                canon.End();
            }

            canon.Flag("potential-band", belief.Potential is not null);
            if (belief.Potential is AttributeBand band)
            {
                canon.Number("potential-low", band.Low);
                canon.Number("potential-high", band.High);
            }
        }

        return canon.ToString();
    }

    private sealed class Canon
    {
        private readonly StringBuilder _builder = new();

        public void Line(string text)
        {
            _builder.Append(text);
            _builder.Append('\n');
        }

        public void Count(string label, int count) =>
            Line(label + " " + count.ToString(CultureInfo.InvariantCulture));

        public void Number(string label, long value) =>
            Line(label + " " + value.ToString(CultureInfo.InvariantCulture));

        public void Flag(string label, bool value) => Line(label + " " + (value ? "1" : "0"));

        public void TextLine(string label, string value)
        {
            Begin(label);
            Field(value);
            End();
        }

        public void TextNumber(string label, string value, int number)
        {
            Begin(label);
            Field(value);
            Space();
            Raw(number.ToString(CultureInfo.InvariantCulture));
            End();
        }

        public void Begin(string label)
        {
            _builder.Append(label);
            _builder.Append(' ');
        }

        public void Field(string value)
        {
            _builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            _builder.Append(':');
            _builder.Append(value);
        }

        public void Space() => _builder.Append(' ');

        public void Raw(string text) => _builder.Append(text);

        public void End() => _builder.Append('\n');

        public override string ToString() => _builder.ToString();
    }
}
