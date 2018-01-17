using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;

namespace NuGetPackageConfigConverter
{
    internal sealed class PackageSystemUpdateCommand
    {
        public const int CommandId = 0x0100;

        public static readonly Guid CommandSet = new Guid("53c7366b-270d-4a41-afb7-ffcf007534a2");

        private readonly Package _package;
        private readonly IPackageManagerConverter _packageConverter;
        private readonly DTE2 _dte;
        private readonly ILogger _logger;

        private PackageSystemUpdateCommand(Package package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _dte = ServiceProvider.GetService(typeof(DTE)) as DTE2;

            var container = ServiceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            _packageConverter = container.GetService<IPackageManagerConverter>();
            _logger = container.GetService<ILogger>();

            if (ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new OleMenuCommand(MenuItemCallback, menuCommandID);
                menuItem.BeforeQueryStatus += MenuItemBeforeQueryStatus;
                commandService.AddCommand(menuItem);
            }
        }

        public static PackageSystemUpdateCommand Instance { get; private set; }

        private IServiceProvider ServiceProvider => _package;

        public static void Initialize(Package package)
        {
            Instance = new PackageSystemUpdateCommand(package);
        }

        private void MenuItemBeforeQueryStatus(object sender, EventArgs e)
        {
            var item = (OleMenuCommand)sender;

            item.Visible = _packageConverter.NeedsConversion(_dte.Solution);
        }

        private async void MenuItemCallback(object sender, EventArgs e)
        {
            await _packageConverter.ConvertAsync(_dte.Solution,_logger);
        }
    }
}
