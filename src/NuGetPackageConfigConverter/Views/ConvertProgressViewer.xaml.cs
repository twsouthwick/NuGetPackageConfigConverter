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
        }
    }
}
