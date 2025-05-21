using EPDM.Interop.epdm;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace CADShark.OpenBatchPDM.AddIn
{
    public class OpenBatchTabView
    {
        public void OpenBatchCustomTabView(object poCmdMgr, OpenBatchTabViewUc uc, long uniqueId)
        {

            const string toolTip = "OpenBatch: перегляд PDF-файла.";

            const string resourceName = "Icon"; // Назва ресурсу
            const string fileExtension = ".png"; // Розширення файлу

            var icon = Properties.Resources.Icon;
            var directoryPath = GetAssemblyDirectory();
            const string fileName = resourceName + fileExtension;
            var iconLocation = Path.Combine(directoryPath, fileName);
            using (var fs = new FileStream(iconLocation, FileMode.Create))
            {
                icon.Save(fs);
            }
            
            // create control
            uc.Dock = DockStyle.Fill;
            // get control id
            var windowHandle = uc.Handle.ToInt64();
            // call to add the tab
            ((IEdmCmdMgr6)poCmdMgr).AddVaultViewTab(windowHandle, "OpenBatch", iconLocation, toolTip, uniqueId.ToString());


        }

        // method to allow the png to be saved in the vault
        private static string GetAssemblyDirectory()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }
    }
}