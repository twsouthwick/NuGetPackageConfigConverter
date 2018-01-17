using Microsoft.VisualStudio.PlatformUI;
using System;
using System.ComponentModel;
using System.Threading;

namespace NuGetPackageConfigConverter
{
    public partial class ConvertProgressViewer : DialogWindow
    {
        private readonly Action _close;

        public ConvertProgressViewer(ConverterUpdateViewModel model, Action close)
        {
            _close = close;

            DataContext = model;
            InitializeComponent();
        }

        public ConverterUpdateViewModel Model => DataContext as ConverterUpdateViewModel;

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;

            IsCloseButtonEnabled = false;
            Model.Status = "Cancelling...";

            _close();
        }

        public bool? ShowModal(CancellationToken token)
        {
            using (token.Register(() => Dispatcher.Invoke(Hide)))
            {
                return ShowModal();
            }
        }
    }
}
