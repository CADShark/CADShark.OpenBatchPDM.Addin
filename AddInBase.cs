using CADShark.Common.DBManager.Data;
using CADShark.Common.DBManager.Models;
using CADShark.Common.Feedback;
using CADShark.Common.Logging;
using CADShark.Common.MultiConverter;
using CADShark.Common.SolidWorks;
using CADShark.Common.SolidWorks.Core;
using CADShark.Common.SolidWorks.Documents;
using CADShark.Common.SolidworksPDM;
using EPDM.Interop.epdm;
using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FileInfo = CADShark.Common.SolidworksPDM.FileInfo;


namespace CADShark.OpenBatchPDM.AddIn
{
    [ComVisible(true)]
    [Guid("9E43E8E9-5025-4959-991B-7DD78EF17BE0")]
    public class AddInBase : IEdmAddIn5
    {
        private const int CreatePdfCmdId = 1;
        private const int GetPdfCmdId = 2;
        private const int MenuAdministration = 6;
        private const int MenuInfoForm = 7;
        private const int MenuFeedBack = 8;
        private EdmVault5 _vault;
        private IDbManager _db;
        private ISldWorksInstManager _instManage;
        private int _mlParentWnd;
        private static SldWorks _swApp;
        private List<FileInfo> _files;
        private string _connectionString;
        private PdmInstanceManager _instance;
        private static readonly CadLogger Logger = CadLogger.GetLogger(nameof(AddInBase));

        public void GetAddInInfo(ref EdmAddInInfo poInfo, IEdmVault5 poVault, IEdmCmdMgr5 poCmdMgr)
        {
            try
            {
                poInfo.mbsAddInName = "OpenBatch Converter";
                poInfo.mbsCompany = "CADShark";
                poInfo.mbsDescription =
                    "An add-in for SOLIDWORKS PDM that creates PDF, DXF files from SOLIDWORKS documents and stores data in a database.";
                poInfo.mlAddInVersion = 12042024;
                poInfo.mlRequiredVersionMajor = 31;
                poInfo.mlRequiredVersionMinor = 5;

                poCmdMgr.AddCmd(CreatePdfCmdId, @"OpenBatch\Сформувати комплект PDF-файлів",
                    (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);
                poCmdMgr.AddCmd(GetPdfCmdId, @"OpenBatch\Вивантажити комплект PDF-файлів",
                    (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);

                //Administration menu
                poCmdMgr.AddCmd(MenuAdministration, @"Settings",
                    (int)EdmMenuFlags.EdmMenu_NeverInContextMenu + (int)EdmMenuFlags.EdmMenu_Administration);
                poCmdMgr.AddCmd(MenuInfoForm, @"Feedback",
                    (int)EdmMenuFlags.EdmMenu_NeverInContextMenu + (int)EdmMenuFlags.EdmMenu_Administration);
                poCmdMgr.AddCmd(MenuFeedBack, @"Leave a review",
                    (int)EdmMenuFlags.EdmMenu_NeverInContextMenu + (int)EdmMenuFlags.EdmMenu_Administration);

                AddAllHooks(poCmdMgr);
            }
            catch (Exception e)
            {
                MessageBox.Show($@"GetAddInInfo {e.Message}");
            }
        }

        public void OnCmd(ref EdmCmd poCmd, ref EdmCmdData[] ppoData)
        {
            try
            {
                string filePath;
                _vault = (EdmVault5)poCmd.mpoVault;
                _mlParentWnd = poCmd.mlParentWnd;

                _instance = new PdmInstanceManager(_vault, _mlParentWnd);

                switch (poCmd.mlCmdID)
                {
                    case CreatePdfCmdId:
                    {
                        var paths = new List<string>();

                        for (var i = 0; i < ppoData.Length; i++)
                        {
                            //ID of parent folder of the selected file or folder
                            var folderObject = _vault.GetObject(EdmObjectType.EdmObject_Folder,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID3);
                            var ef = (IEdmFolder5)folderObject;

                            var fileObject = _vault.GetObject(EdmObjectType.EdmObject_File,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID1);
                            filePath = Path.Combine(ef.LocalPath, fileObject.Name);
                            //filePath = ef.LocalPath + "\\" + fileObject.Name;

                            paths.Add(filePath);
                        }

                        if (paths.Any())
                        {
                            GetConnString();
                            if (!CheckConn()) return;
                            BatchCreator(paths, ".SLDDRW");
                        }

                        break;
                    }
                    case GetPdfCmdId:
                    {
                        var paths = new List<string>();

                        for (var i = 0; i < ppoData.Length; i++)
                        {
                            var folderObject = _vault.GetObject(EdmObjectType.EdmObject_Folder,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID3);
                            var ef = (IEdmFolder5)folderObject;

                            var fileObject = _vault.GetObject(EdmObjectType.EdmObject_File,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID1);
                            filePath = Path.Combine(ef.LocalPath, fileObject.Name);
                            //filePath = ef.LocalPath + "\\" + fileObject.Name;
                            paths.Add(filePath);
                        }

                        if (paths.Any())
                        {
                            //Init Connection string
                            GetConnString();

                            if (!CheckConn()) return;
                            GetFiles(paths);
                        }

                        break;
                    }
                    case MenuAdministration:
                    {
                        new SettingsForm(_vault).ShowDialog();

                        break;
                    }
                    case MenuInfoForm:
                    {
                        new InfoForm().ShowDialog();

                        break;
                    }
                    case MenuFeedBack:
                    {
                        new FeedbackWindow().ShowDialog();
                        break;
                    }
                }
            }
            catch
                (Exception e)
            {
                MessageBox.Show(
                    $@"Нам дуже прикро, але в програмі сталася помилка.
Для вирішення питання, зв'яжіться з техпідтримкою.
{e.Message} 
{e.Source}",
                    @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        public void GetFiles(List<string> filePath)
        {
            try
            {
                var dig = new FolderBrowserDialog();
                dig.ShowDialog();
                dig.Description = @"Виберіть папку для збереження PDF-файлів.";

                if (string.IsNullOrEmpty(dig.SelectedPath)) return;
                var savePath = dig.SelectedPath;

                var countItems = 0;
                //_instance = new PdmInstanceManager(_vault, _mlParentWnd);

                foreach (var path in filePath)
                {
                    _files = _instance.ListFiles(path).Where(drw => drw.FileName.ToUpper().EndsWith(@".SLDDRW"))
                        .OrderBy(x => x.FileName).ToList();
                }


                var incomingCount = _files.Count;

                if (incomingCount == 0) return;

                _db = new DbManager(_connectionString);

                if (_db == null)
                {
                    MessageBox.Show(@"First, set up a connection to the database.");
                    return;
                }

                foreach (var file in _files)
                {
                    var items = _db.Items().Where(item =>
                        item.DocumentId == file.DocumentId && item.Version == file.CurrentVersion);

                    foreach (var item in items)
                    {
                        var folderPath = Path.Combine(savePath, item.FileName);
                        
                        var blob = _db.GetBlobById(item.Id);
                        if (blob == null)
                        {
                            MessageBox.Show($@"Error to save file: {item.FileName}");
                            continue;
                        }
                        File.WriteAllBytes(folderPath, blob);
                        countItems++;
                    }
                }


                if (countItems == incomingCount)
                    MessageBox.Show($@"Вивантажено {countItems} з {incomingCount} файлів.",
                        @"Звіт по вивантаженим файлам.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show(
                        $@"Увага! Вивантажено {countItems} з {incomingCount} файлів.
Вивантажено не повний комплект PDF-файлів.",
                        @"Звіт по вивантаженим файлам.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        public void BatchCreator(List<string> filesPath, string typeDoc)
        {
            var count = 0;

            try
            {
                //_instance = new PdmInstanceManager(_vault, _mlParentWnd);
                foreach (var filePath in filesPath)
                {
                    _files = _instance.ListFiles(filePath);
                }

                if (_files.Count == 0)
                {
                    MessageBox.Show(@"Відсутні файли для перетворення. 1 ",
                        @"Інформація про процес перетворенню файлів.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                //Get files which consist variable Revision
                //var fileInfos = _files.Where(file => !string.IsNullOrEmpty(file.Revision)).ToList();


                //if (fileInfos.Count == 0)
                //{
                //    MessageBox.Show(@"Відсутні файли для перетворення.", @"Інформація про процес перетворенню файлів.",
                //        MessageBoxButtons.OK, MessageBoxIcon.Information);
                //    return;
                //}

                _db = new DbManager(_connectionString);

                if (_db == null)
                {
                    MessageBox.Show(@"First, set up a connection to the database.");
                    return;
                }

                var notInDbSet = _files.Where(fileInfo => !_db.Items().Any(item =>
                    item.DocumentId == fileInfo.DocumentId && item.Version == fileInfo.CurrentVersion)).ToList();

                notInDbSet = notInDbSet.Where(x => x.FileName.Contains(typeDoc)).ToList();
                if (notInDbSet.Count == 0)
                {
                    MessageBox.Show(@"Відсутні файли для перетворення. 2 ",
                        @"Інформація про процес перетворенню файлів.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _instManage = new SldWorksInstManager();
                _swApp = _instManage.GetNewInstance();
                _swApp.Visible = true;

                IAssemblyDocument model = new AssemblyDocument(_swApp);

                var convert = new ConvertBuilder(_swApp);


                foreach (var file in notInDbSet)
                {
                    if (!_instance.GetFileCopy(file.FilePath)) continue;

                    var fileType = Path.GetExtension(file.FilePath);
                    switch (fileType)
                    {
                        case @".SLDDRW":
                        {
                            model.OpenFile(file.FilePath, OpenDocumentOptions.ReadOnly);
                            convert.PathBuilder(file.FilePath, "PDF", null, AppDataTemp());
                            var pdfPath = ConvertBuilder.FilePath;
                            model.SuppressUpdates(false);
                            convert.ConvertToPdf();

                            //var qrCode = GetHyperlink("explore", file);
                            //var qrCode = GetHyperlink(file, "explore");
                            //GetBarcode(pdfPath, qrCode, out var outFileName);

                            var blob = ReadAllBytes(pdfPath);
                            //var blob = ReadAllBytes(!string.IsNullOrEmpty(outFipdfPathleName) ? outFileName : pdfPath);
                            //if (blob == null)
                            //{
                            //    Logger.Error($"Bytes is null outFileName {outFileName}");
                            //    continue;
                            //}

                            var newItem = new Items
                            {
                                DocumentId = file.DocumentId,
                                FileName = Path.GetFileName(pdfPath),
                                Revision = file.Revision,
                                Version = file.CurrentVersion,
                                DocType = "PDF",
                                Blob = blob
                            };

                            _db.Add(newItem);
                            count++;
                            break;
                        }
                        case ".SLDPRT":
                        {
                            model.OpenFile(file.FilePath, OpenDocumentOptions.ReadOnly);

                            if (!convert.IsSheetMetalComponent())
                            {
                                _swApp.CloseDoc(file.FilePath);
                                continue;
                            }

                            var vConfig = model.GetDerivedConfig();
                            model.SuppressUpdates(false);

                            foreach (var config in vConfig)
                            {
                                convert.PathBuilder(file.FilePath, "DXF", config, AppDataTemp());
                                convert.ConvertToDxf(true);

                                var dxfPath = ConvertBuilder.FilePath;
                                var blob = ReadAllBytes(dxfPath);

                                var newItem = new Items
                                {
                                    DocumentId = file.DocumentId,
                                    FileName = Path.GetFileName(dxfPath),
                                    Revision = file.Revision,
                                    Version = file.CurrentVersion,
                                    Config = config,
                                    DocType = "DXF",
                                    Blob = blob
                                };

                                _db.Add(newItem);
                            }

                            count++;
                            break;
                        }
                    }

                    _swApp.CloseDoc(file.FilePath);
                }

                DeleteTempFolder();

                _swApp.CloseAllDocuments(true);
                _swApp.ExitApp();

                _instManage.ReleaseInstance(_swApp);

                MessageBox.Show($@"Створено {count} файлів.", @"Звіт по перетворенню файлів.",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $@"Нам дуже прикро, але в програмі сталася помилка.
Для вирішення питання, зв'яжіться з техпідтримкою.
{e.Message} 
{e.Source}",
                    @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                MessageBox.Show(e.StackTrace);
                MessageBox.Show(e.Source);
                Logger.Error(e.Message);
            }
        }

        public string AppDataTemp()
        {
            var appdataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            appdataPath = Path.Combine(appdataPath, @"CADShark\Temp");
            return appdataPath;
        }

        public byte[] ReadAllBytes(string filePath)
        {
            byte[] fileBytes = null;
            // Check if the file exists
            if (File.Exists(filePath))
                try
                {
                    // Read all bytes from the file
                    fileBytes = File.ReadAllBytes(filePath);
                }
                catch (IOException e)
                {
                    Logger.Error($@"An IO exception occurred: {e.Message}");
                }
                catch (UnauthorizedAccessException e)
                {
                    Logger.Error($@"Access to the file is unauthorized: {e.Message}");
                }
                catch (Exception e)
                {
                    Logger.Error($@"@An error occurred: {e.Message}");
                }
            else
                Logger.Error(@"File does not exist.");

            return fileBytes;
        }

        private void GetConnString()
        {
            var projectDictionary = _vault.GetDictionary("OpenBatch", false);
            if (projectDictionary == null) return;

            var pos = projectDictionary.StringFindKeys("ConnectionString");

            while (!pos.IsNull)
            {
                projectDictionary.StringGetNextAssoc(pos, out _, out var value);
                _connectionString = value;
            }
        }

        private bool CheckConn()
        {
            if (!string.IsNullOrEmpty(_connectionString)) return true;
            MessageBox.Show(
                $@"Не налаштоване підключення до бази даних.
Зверніться до вашого адміністратора для налаштування підключення.",
                @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private string GetHyperlink(FileInfo file, string action)
        {
            return
                $@"conisio://{_vault.Name}/{action}?projectid={file.DocumentId}&documentid={file.FolderId}&objecttype={1}";
        }

        private void DeleteTempFolder()
        {
            if (Directory.Exists(AppDataTemp()))
                new DirectoryInfo(AppDataTemp()).Delete(true);
        }

        private void GetBarcode(string filePath, string code, out string outFileName)
        {
            outFileName = null;
            var exePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            exePath = Path.Combine(exePath, @"CADShark", "OpenBatch", "CADShark.Common.PDF.exe");
            if (!File.Exists(exePath)) Logger.Error("CreateBarcode CADShark.Common.PDF.exe does not exists.");

            using (var process = new Process())
            {
                process.StartInfo.FileName = exePath;
                process.StartInfo.Arguments = $"{filePath} {code}";
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit();
            }

            var directoryName = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (directoryName == null) return;
            var outFolderName = Path.Combine(directoryName, "QR");
            outFileName = Path.Combine(outFolderName, fileName);
        }

        private void AddAllHooks(IEdmCmdMgr5 poCmdMgr)
        {
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_CardButton);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_CardInput);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_CardListSrc);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_InstallAddIn);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_Menu);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostAdd);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostAddFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostCopy);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostCopyFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostDelete);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostDeleteFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostGet);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostLock);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostMove);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostMoveFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostRename);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostRenameFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostShare);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostState);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostUndoLock);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostUnlock);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreAdd);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreAddFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreCopy);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreCopyFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreDelete);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreDeleteFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreGet);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreLock);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreMove);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreMoveFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreRename);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreRenameFolder);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreShare);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreState);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreUndoLock);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreUnlock);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_SerialNo);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_UninstallAddIn);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_TaskDetails);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_SelectItem);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_DeSelectItem);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_ActivateAPITab);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_UserTabDelete);
            poCmdMgr.AddHook(EdmCmdType.EdmCmd_PreExploreInit);
        }
    }
}