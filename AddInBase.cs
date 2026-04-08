//using CADShark.Common.Logging;
using CADShark.Common.MultiConverter;
using CADShark.Common.MultiConverter.Core;
using CADShark.Common.MultiConverter.Services;
using CADShark.Common.SolidWorks;
using CADShark.Common.SolidWorks.Core;
using CADShark.Common.SolidWorks.Documents;
using CADShark.Common.SolidworksPDM;
using CADShark.OpenBatchPDM.Addin;
using CADShark.OpenBatchPDM.Addin.OpenVaultAPI;
using EPDM.Interop.epdm;
using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FileInfoModel = CADShark.Common.SolidworksPDM.FileInfoModel;


namespace CADShark.OpenBatchPDM.AddIn
{
    [ComVisible(true)]
    [Guid("9E43E8E9-5025-4959-991B-7DD78EF17BE0")]
    public class AddInBase : IEdmAddIn5
    {
        private const int CreatePdfCmdId = 1;
        private const int GetPdfCmdId = 2;
        private const int CreateDXFCmdId = 3;
        private EdmVault5 _vault;
        private ISldWorksInstManager _instManage;
        private int _mlParentWnd;
        private static SldWorks _swApp;
        private List<FileInfoModel> _files;
        private PdmInstanceManager _instance;
        //private static readonly CadLogger Logger = CadLogger.GetLogger<AddInBase>();

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

                poCmdMgr.AddCmd(CreatePdfCmdId, @"OpenBatch\Сформувати PDF-файл",
                    (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);
                poCmdMgr.AddCmd(CreateDXFCmdId, @"OpenBatch\Сформувати DXF-файл",
    (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);

                //poCmdMgr.AddCmd(GetPdfCmdId, @"OpenBatch\Вивантажити комплект PDF-файлів",
                //    (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);

                //Register to receive a notification when a file has changed state
                poCmdMgr.AddHook(EdmCmdType.EdmCmd_PostState);
            }
            catch (Exception e)
            {
                MessageBox.Show($@"GetAddInInfo {e.Message}");
            }
        }

        public void OnCmd(ref EdmCmd poCmd, ref EdmCmdData[] ppoData)
        {
            //try
            //{
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
                            var folderObject = _vault.GetObject(EdmObjectType.EdmObject_Folder,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID3);
                            var ef = (IEdmFolder5)folderObject;

                            var fileObject = _vault.GetObject(EdmObjectType.EdmObject_File,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID1);
                            filePath = Path.Combine(ef.LocalPath, fileObject.Name);

                            paths.Add(filePath);

                            BatchCreator(paths, 0);
                        }

                        break;
                    }
                case CreateDXFCmdId:
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

                            paths.Add(filePath);

                            BatchCreator(paths, 1);
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
                        break;
                    }


            }

            //if (poCmd.meCmdType == EdmCmdType.EdmCmd_PostState)
            //{
            //    foreach (EdmCmdData AffectedFile in ppoData)
            //    {
            //        if (AffectedFile.mbsStrData2 == "На Утверждении")
            //        { 
            //            MessageBox.Show($@"Файл {AffectedFile.mbsStrData1} изменил состояние на 'На Утверждении'.", @"Изменение состояния файла.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //        }
            //    }

            //}


            //}
            //catch
            //    (Exception e)
            //{
            //    MessageBox.Show(
            //        $@"Нам дуже прикро, але в програмі сталася помилка.
            //        Для вирішення питання, зв'яжіться з техпідтримкою.
            //        {e.Message} 
            //        {e.Source}",
            //        @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    throw;
            //}
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

                foreach (var path in filePath)
                {
                    _files = _instance.ListFiles(path).Where(drw => drw.FileName.ToUpper().EndsWith(@".SLDDRW")).OrderBy(x => x.FileName).ToList();
                }

                var incomingCount = _files.Count;

                if (incomingCount == 0) return;


                foreach (var file in _files)
                {
                    //var items = _db.Items().Where(item =>
                    //    item.DocumentId == file.DocumentId && item.Version == file.CurrentVersion);

                    //foreach (var item in items)
                    //{
                    //    var folderPath = Path.Combine(savePath, item.FileName);


                    //    var blob = _db.GetBlobById(item.Id);
                    //    if (blob == null)
                    //    {
                    //        MessageBox.Show($@"Error to save file: {item.FileName}");
                    //        continue;
                    //    }
                    //    File.WriteAllBytes(folderPath, blob);
                    countItems++;
                    //}
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

        public void BatchCreator(List<string> filesPath, int param)
        {
            var count = 0;
            try
            {
                foreach (var filePath in filesPath)
                {
                    _files = _instance.ListFiles(filePath);
                }
                //return;

                if (_files.Count == 0)
                {
                    MessageBox.Show(@"Відсутні файли для перетворення. 1 ",
                        @"Інформація про процес перетворенню файлів.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _instManage = new SldWorksInstManager();
                _swApp = _instManage.GetNewInstance();

                IAssemblyDocument model = new AssemblyDocument(_swApp);

                var factory = new ConverterFactory(_swApp);
                var pdfConverter = factory.Create(ExportFormat.Pdf);
                var dxfConverter = factory.Create(ExportFormat.Dxf);
                IFilePathBuilder pathBuilder = new FilePathBuilder();
                var client = new Client();
                foreach (var file in _files)
                {
                    if (!_instance.GetFileCopy(file.FilePath)) continue;
                    var fileType = Path.GetExtension(file.FilePath);
                    switch (fileType)
                    {
                        case @".SLDDRW":
                            {
                                if (param == 1) continue;
                                //var request = new SearchRequest
                                //{
                                //    Filters = new[]
                                //    {
                                //        new Filter { AttributeId = 2001, Value = file.DocumentId.ToString() },
                                //        new Filter { AttributeId = 2002, Value = file.CurrentVersion.ToString() }
                                //     }
                                //};

                                //int[] objectIds = client.SearchObjectsAsync(request).GetAwaiter().GetResult();

                                //if (objectIds != null) continue;

                                //MessageBox.Show($@"Знайдено {objectIds.Length} об'єктів з атрибутами DocumentId = {file.DocumentId} та Version = {file.CurrentVersion}.", @"Результат пошуку об'єктів.", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                var modelDoc = model.OpenFile(file.FilePath, OpenDocumentOptions.ReadOnly);
                                var pdfPath = pathBuilder.Build(file.FilePath, ExportFormat.Pdf, null, AppDataTemp());
                                var status = pdfConverter.Export(modelDoc, pdfPath);

                                //if(!status) continue;

                                var blob = BlobReader.ReadAllBytes(pdfPath);
                                var pdfName = Path.GetFileName(pdfPath);
                                var objectId = client.CreateObjectAsync(1742).GetAwaiter().GetResult();

                                //Обозначение
                                //_ = client.AddAttribute(objectId, 9, "");
                                //Наименование
                                //_ = client.AddAttribute(objectId, 10, "");
                                //FileID
                                _ = client.AddAttribute(objectId, 2001, file.DocumentId.ToString());
                                //Version
                                _ = client.AddAttribute(objectId, 2002, file.CurrentVersion.ToString());
                                //File
                                _ = client.AddAttribute(objectId, 1002, pdfName);
                                //Preview
                                //_ = client.AddAttribute(objectId, 1002, pdfName);

                                //Upload PDF-file 
                                _ = client.WritteBlob(pdfName, blob, objectId, 1002, 0);
                                //upload preview
                                //_ = client.WritteBlob(pdfName, blob, objectId, 1002, 0);

                                break;
                            }
                        case @".SLDPRT":
                            {
                                if (param == 0) continue;
                                //var request = new SearchRequest
                                //{
                                //    Filters = new[]
                                //    {
                                //        new Filter { AttributeId = 2001, Value = file.DocumentId.ToString() },
                                //        new Filter { AttributeId = 2002, Value = file.CurrentVersion.ToString() }
                                //     }
                                //};

                                //int[] objectIds = client.SearchObjectsAsync(request).GetAwaiter().GetResult();

                                //if (objectIds != null) continue;

                                //MessageBox.Show($@"Знайдено {objectIds.Length} об'єктів з атрибутами DocumentId = {file.DocumentId} та Version = {file.CurrentVersion}.", @"Результат пошуку об'єктів.", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                var modelDoc = model.OpenFile(file.FilePath, OpenDocumentOptions.ReadOnly);
                                var dxfPath = pathBuilder.Build(file.FilePath, ExportFormat.Dxf, null, AppDataTemp());
                                var status = dxfConverter.Export(modelDoc, dxfPath);

                                if(!status) continue;

                                var blob = BlobReader.ReadAllBytes(dxfPath);
                                var dxfName = Path.GetFileName(dxfPath);
                                var objectId = client.CreateObjectAsync(1742).GetAwaiter().GetResult();

                                //Обозначение
                                //_ = client.AddAttribute(objectId, 9, "");
                                //Наименование
                                //_ = client.AddAttribute(objectId, 10, "");
                                //FileID
                                _ = client.AddAttribute(objectId, 2001, file.DocumentId.ToString());
                                //Version
                                _ = client.AddAttribute(objectId, 2002, file.CurrentVersion.ToString());
                                //File
                                _ = client.AddAttribute(objectId, 1002, dxfName);
                                //Preview
                                //_ = client.AddAttribute(objectId, 1002, dxfName);

                                //Upload PDF-file 
                                _ = client.WritteBlob(dxfName, blob, objectId, 1002, 0);
                                //upload preview
                                //_ = client.WritteBlob(dxfName, blob, objectId, 1002, 0);

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
                //Logger.Error(e.Message);
            }
        }

        public string AppDataTemp()
        {
            var appdataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            appdataPath = Path.Combine(appdataPath, @"CADShark\Temp");
            return appdataPath;
        }



        private void DeleteTempFolder()
        {
            if (Directory.Exists(AppDataTemp()))
                new DirectoryInfo(AppDataTemp()).Delete(true);
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