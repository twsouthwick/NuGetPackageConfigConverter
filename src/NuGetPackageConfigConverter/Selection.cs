using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;

using static Microsoft.VisualStudio.VSConstants;

namespace NuGetPackageConfigConverter
{
    public static class Selection
    {
        public static T GetSelection<T>()
            where T : class
        {
            var monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
            var result = monitorSelection.GetCurrentSelection(out var ppHier, out var pitemid, out _, out _);

            if (result == S_OK)
            {
                if (Marshal.GetTypedObjectForIUnknown(ppHier, typeof(IVsHierarchy)) is IVsHierarchy selectedHierarchy)
                {
                    try
                    {
                        if (selectedHierarchy.GetProperty(pitemid, (int)__VSHPROPID.VSHPROPID_ExtObject, out var selectedObject) == S_OK)
                        {
                            return selectedObject as T;
                        }
                    }
                    finally
                    {
                        if (ppHier != IntPtr.Zero)
                        {
                            Marshal.Release(ppHier);
                        }
                    }
                }
            }

            return default(T);
        }
    }
}
