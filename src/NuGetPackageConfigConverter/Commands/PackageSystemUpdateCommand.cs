using EnvDTE;
using EnvDTE80;
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

        private PackageSystemUpdateCommand(Package package, IPackageManagerConverter converter)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _packageConverter = converter;
            _package = package;
            _dte = ServiceProvider.GetService(typeof(DTE)) as DTE2;

            var commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
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
            var packageConverter = Clide.ServiceLocator.GlobalLocator.GetService<IPackageManagerConverter>();

            Instance = new PackageSystemUpdateCommand(package, packageConverter);
        }

        private void MenuItemBeforeQueryStatus(object sender, EventArgs e)
        {
            var item = (OleMenuCommand)sender;

            item.Visible = _packageConverter.HasPackageConfig(_dte.Solution.Projects);
        }

        private async void MenuItemCallback(object sender, EventArgs e)
        {
            await _packageConverter.ConvertAsync(_dte.Solution);
        }
    }
}
