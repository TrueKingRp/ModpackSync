namespace ModpackSync.Cli.Commands;

public interface ICommand
{
    string Name { get; }

    string Usage { get; }

    Task ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default);
}