using Microsoft.VisualStudio.PlatformUI;
using System.Threading;

namespace NuGetPackageConfigConverter
{
    public partial class ConvertProgressViewer : DialogWindow
    {
        public ConvertProgressViewer(ConverterUpdateViewModel model)
        {
            DataContext = model;
            InitializeComponent();
        }

        public void ShowModal(CancellationToken token)
        {
            using (token.Register(() => Dispatcher.Invoke(Hide)))
            {
                ShowModal();
            }

            MessageDialog.Show("Conversion complete", "Conversion to project.json dependency is complete. Some files may have been removed by uninstalling the packages and not added back. Please ensure project builds and runs before committing any changes.", MessageDialogCommandSet.Ok);
        }
    }
}
