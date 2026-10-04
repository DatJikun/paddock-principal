using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Pool;
using Paddock.Domain.Codec;
using Paddock.Domain.Pool;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Commands;

/// <summary>
/// How one kind of command is saved: a versioned tag (<c>name/version</c>) and a codec for the fields that are
/// specific to it. The manager, submission number and issue date are columns of the command log, not part of the body.
/// </summary>
public sealed class CommandCodecEntry
{
    private readonly Func<ICommand, string> _encode;
    private readonly Func<string, ManagerId, DateOnly, ICommand> _decode;

    private CommandCodecEntry(string tag, Type commandType, Func<ICommand, string> encode, Func<string, ManagerId, DateOnly, ICommand> decode)
    {
        Tag = tag;
        CommandType = commandType;
        _encode = encode;
        _decode = decode;
    }

    public string Tag { get; }

    public Type CommandType { get; }

    /// <summary>
    /// An entry for <typeparamref name="T"/>. <paramref name="decode"/> builds the command from its body, the manager
    /// and the issue date; the submission number is applied by the codec through <see cref="ICommand.WithSubmissionNumber"/>.
    /// </summary>
    public static CommandCodecEntry For<T>(string tag, Func<T, string> encode, Func<string, ManagerId, DateOnly, T> decode)
        where T : ICommand
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(encode);
        ArgumentNullException.ThrowIfNull(decode);
        return new CommandCodecEntry(tag, typeof(T), command => encode((T)command), (body, manager, issued) => decode(body, manager, issued));
    }

    internal string Encode(ICommand command) => _encode(command);

    internal ICommand Decode(string body, ManagerId manager, DateOnly issuedOn) => _decode(body, manager, issuedOn);
}

/// <summary>
/// The command rows of a save. An explicit registry: a command type without an entry cannot be saved, and a tag without
/// an entry cannot be loaded (<see cref="UnknownTagException"/>), so a command is never guessed or dropped. A command that
/// changes shape gets a new tag version and the old entry stays, so older saves keep loading. A system that adds commands
/// adds its entries to <see cref="Production"/>; a test fails until it does.
/// </summary>
public sealed class CommandCodec
{
    private const string Kind = "command";

    private readonly Dictionary<string, CommandCodecEntry> _byTag = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, CommandCodecEntry> _byType = [];

    public CommandCodec(IEnumerable<CommandCodecEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (!_byTag.TryAdd(entry.Tag, entry))
            {
                throw new ArgumentException("Two command codecs use the tag '" + entry.Tag + "'.", nameof(entries));
            }

            if (!_byType.TryAdd(entry.CommandType, entry))
            {
                throw new ArgumentException("Two command codecs handle " + entry.CommandType.Name + ".", nameof(entries));
            }
        }
    }

    /// <summary>
    /// The commands this build can save: the core ones listed below (inbox, talent pool) and the ones every career module brings
    /// (<see cref="Career.ICareerModule.CommandCodecs"/>). A system that joins <see cref="Career.CareerModules.Default"/> is saved
    /// without an edit here.
    /// </summary>
    public static CommandCodec Production { get; } = new(
    [
        // T42 and T43 were merged (#163, #162) before they became career modules. Each moves its line into its own module's
        // CommandCodecs when it joins CareerModules.Default; until then these two stay here so their commands are still saved.
        .. Paddock.Application.Development.DevelopmentCommandCodecs.Entries,
        .. Paddock.Application.Supply.SupplyCommandCodecs.Entries,
        .. Career.CareerModules.Default.SelectMany(module => module.CommandCodecs),
        CommandCodecEntry.For<ResolveInboxItemCommand>(
            "inbox.resolve/1",
            command => FlatJson.Write(("itemId", command.ItemId), ("optionId", command.OptionId)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "itemId", "optionId");
                return new ResolveInboxItemCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    ItemId = fields.String("itemId"),
                    OptionId = fields.String("optionId"),
                };
            }),
        CommandCodecEntry.For<DismissInboxItemCommand>(
            "inbox.dismiss/1",
            command => FlatJson.Write(("itemId", command.ItemId)),
            (body, manager, issued) => new DismissInboxItemCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                ItemId = FlatJson.Read(body, "itemId").String("itemId"),
            }),
        CommandCodecEntry.For<ExpireInboxItemCommand>(
            "inbox.expire/1",
            command => FlatJson.Write(("itemId", command.ItemId)),
            (body, manager, issued) => new ExpireInboxItemCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                ItemId = FlatJson.Read(body, "itemId").String("itemId"),
            }),
        CommandCodecEntry.For<AssignScoutFocusCommand>(
            "pool.scoutFocus/1",
            command => FlatJson.Write(("person", command.PersonHandle ?? string.Empty)),
            (body, manager, issued) =>
            {
                var person = FlatJson.Read(body, "person").String("person");
                return new AssignScoutFocusCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    PersonHandle = person.Length == 0 ? null : person,
                };
            }),
        CommandCodecEntry.For<FundJuniorCommand>(
            "pool.fundJunior/1",
            command => FlatJson.Write(("person", command.PersonHandle), ("programme", command.Programme.ToString())),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "person", "programme");
                return new FundJuniorCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    PersonHandle = fields.String("person"),
                    Programme = Enum.Parse<JuniorProgramme>(fields.String("programme")),
                };
            }),
        CommandCodecEntry.For<SignPoolDriverCommand>(
            "pool.signDriver/1",
            command => FlatJson.Write(("person", command.PersonHandle), ("role", command.Role.ToString())),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "person", "role");
                return new SignPoolDriverCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    PersonHandle = fields.String("person"),
                    Role = Enum.Parse<PoolSigningRole>(fields.String("role")),
                };
            }),
    ]);

    public IReadOnlyCollection<Type> SavedTypes => _byType.Keys;

    /// <summary>The tag and body of <paramref name="command"/>. Throws when the type has no entry.</summary>
    public TaggedText Encode(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_byType.TryGetValue(command.GetType(), out var entry))
        {
            throw new InvalidOperationException(
                "No save codec for command type '" + command.GetType().Name + "'. Add an entry to CommandCodec.Production.");
        }

        return TaggedText.Of(entry.Tag, entry.Encode(command));
    }

    /// <summary>Rebuilds a logged command. The result has the saved submission number.</summary>
    public ICommand Decode(string tag, string body, ManagerId manager, long submissionNumber, DateOnly issuedOn)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(body);
        if (!_byTag.TryGetValue(tag, out var entry))
        {
            throw new UnknownTagException(Kind, tag);
        }

        if (submissionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(submissionNumber), submissionNumber, "A logged command is numbered.");
        }

        try
        {
            return entry.Decode(body, manager, issuedOn).WithSubmissionNumber(submissionNumber);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("A stored '" + tag + "' command is not valid: " + exception.Message, exception);
        }
    }
}
