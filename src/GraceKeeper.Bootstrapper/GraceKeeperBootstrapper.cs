using System;
using WixToolset.Mba.Core;

[assembly: BootstrapperApplicationFactory(typeof(GraceKeeper.Bootstrapper.GraceKeeperBootstrapperFactory))]

namespace GraceKeeper.Bootstrapper;

public sealed class GraceKeeperBootstrapperFactory : BaseBootstrapperApplicationFactory
{
    protected override IBootstrapperApplication Create(IEngine engine, IBootstrapperCommand bootstrapperCommand)
    {
        return new GraceKeeperBootstrapper(engine, bootstrapperCommand);
    }
}

public sealed class GraceKeeperBootstrapper : BootstrapperApplication
{
    private readonly IBootstrapperCommand _command;

    public GraceKeeperBootstrapper(IEngine engine, IBootstrapperCommand command)
        : base(engine)
    {
        _command = command;
    }

    protected override void Run()
    {
        var app = new App(engine: this.engine, command: _command, ba: this);
        app.Run();
        this.engine.Quit(0);
    }
}
