using CADShark.Common.DBManager.Data;
using CADShark.Common.DBManager.Models;
using CADShark.Common.Logging;
using CADShark.Common.MultiConverter;
using CADShark.Common.SolidWorks;
using CADShark.Common.SolidWorks.Core;
using EPDM.Interop.epdm;
using EPDM.Interop.EPDMResultCode;
using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CADShark.OpenBatchPDM.Addin
{
    [ComVisible(true)]
    [Guid("9E43E8E9-5025-4959-991B-7DD78EF17BE0")]
    public class AddInBase : IEdmAddIn5
    {
        private const int CreatePdfCmdId = 1;

        private const int GetPdfCmdId = 2;

        //private const int MenuFeedback = 3;
        //private const int MenuInfo = 4;
        private const int MenuAdministration = 6;
        private EdmVault5 _vault;
        private IAddDbContext _db;
        private int _mlParentWnd;
        private static SldWorks _swApp;
        private List<FileInfo> _files;
        private string _connectionString;
        private static string _localPath, _drawPath;


        private static readonly CadLogger Logger = CadLogger.GetLogger(className: nameof(AddInBase));

        public void GetAddInInfo(ref EdmAddInInfo poInfo, IEdmVault5 poVault, IEdmCmdMgr5 poCmdMgr)
        {
            poInfo.mbsAddInName = "OpenBatch Converter";
            poInfo.mbsCompany = "CADShark";
            poInfo.mbsDescription =
                "An add-in for SOLIDWORKS PDM that creates PDF files from SOLIDWORKS drawings and stores data in a database.";
            poInfo.mlAddInVersion = 29022024;
            poInfo.mlRequiredVersionMajor = 31;
            poInfo.mlRequiredVersionMinor = 5;

            poCmdMgr.AddCmd(CreatePdfCmdId, @"Сформувати комплект PDF-файлів",
                (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);
            poCmdMgr.AddCmd(GetPdfCmdId, @"Вивантажити комплект PDF-файлів",
                (int)EdmMenuFlags.EdmMenu_OnlyFiles + (int)EdmMenuFlags.EdmMenu_OnlySingleSelection);
            poCmdMgr.AddCmd(MenuAdministration, @"Налаштування", (int)EdmMenuFlags.EdmMenu_Administration);
        }

        public void OnCmd(ref EdmCmd poCmd, ref EdmCmdData[] ppoData)
        {
            try
            {
                _vault = (EdmVault5)poCmd.mpoVault;
                _mlParentWnd = poCmd.mlParentWnd;

                switch (poCmd.mlCmdID)
                {
                    case CreatePdfCmdId:
                    {
                        for (var i = 0; i < ppoData.Length; i++)
                        {
                            //ID of parent folder of the selected file or folder
                            var folderObject = _vault.GetObject(EdmObjectType.EdmObject_Folder,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID3);
                            var ef = (IEdmFolder5)folderObject;

                            var fileObject = _vault.GetObject(EdmObjectType.EdmObject_File,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID1);
                            var fileName = ef.LocalPath + "\\" + fileObject.Name;

                            //Init Connection string
                            GetConnString();

                            if (!CheckConn()) return;
                            BatchCreator(fileName);
                        }

                        break;
                    }
                    case GetPdfCmdId:
                    {
                        for (var i = 0; i < ppoData.Length; i++)
                        {
                            var folderObject = _vault.GetObject(EdmObjectType.EdmObject_Folder,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID3);
                            var ef = (IEdmFolder5)folderObject;

                            var fileObject = _vault.GetObject(EdmObjectType.EdmObject_File,
                                ((EdmCmdData)ppoData.GetValue(i)).mlObjectID1);
                            var fileName = ef.LocalPath + "\\" + fileObject.Name;

                            //Init Connection string
                            GetConnString();

                            if (!CheckConn()) return;
                            GetFiles(fileName);
                        }

                        break;
                    }
                    case MenuAdministration:
                    {
                        new SettingsForm(_vault).ShowDialog();

                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $"Нам дуже прикро, але в програмі сталася помилка.\nДля вирішення питання, зв'яжіться з техпідтримкою.\n{e.Message}",
                    @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        public void GetFiles(string filePath)
        {
            var dig = new FolderBrowserDialog();
            dig.ShowDialog();
            dig.Description = @"Виберіть папку для збереження PDF-файлів.";

            if (string.IsNullOrEmpty(dig.SelectedPath)) return;
            var savePath = dig.SelectedPath;

            var countItems = 0;

            ListFiles(filePath);

            foreach (var VARIABLE in _files)
            {
                Logger.Trace(VARIABLE.FileName);
            }

            var incomingCount = _files.Count;

            if (incomingCount == 0) return;

            _db = new AddDbContext(_connectionString);

            foreach (var file in _files)
            {
                var items = _db.Items.Where(item => item.DocumentId == file.DocumentId);
                foreach (var item in items)
                {
                    var folderPath = Path.Combine(savePath, item.FileName);
                    try
                    {
                        File.WriteAllBytes(folderPath, item.Blob);
                        countItems++;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(
                            $"Нам дуже прикро, але в програмі сталася помилка.\nДля вирішення питання, зв'яжіться з техпідтримкою.\n{e.Message}",
                            @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Logger.Error(e.Message);
                        throw;
                    }
                }
            }

            if (countItems == incomingCount)
            {
                MessageBox.Show($"Вивантажено {countItems} з {incomingCount} файлів.", @"Звіт по вивантаженю файлів.",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Увага! Вивантажено {countItems} з {incomingCount} файлів.\nВивантажено не повний комплек PDF-файлі.",
                    @"Звіт по вивантаженю файлів.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void BatchCreator(string filePath)
        {
            try
            {
                ListFiles(filePath);

                if (_files.Count == 0)
                {
                    MessageBox.Show(@"Відсутні файли для перетворення.", @"Інформація про процес перетворенню файлів.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var fileInfos = _files.Where(file => !string.IsNullOrEmpty(file.Revision)).ToList();


                if (fileInfos.Count == 0)
                {
                    MessageBox.Show(@"Відсутні файли для перетворення.", @"Інформація про процес перетворенню файлів.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _db = new AddDbContext(_connectionString);

                var notInDbSet = fileInfos.Where(fileInfo => !_db.Items.Any(item =>
                    item.DocumentId == fileInfo.DocumentId && item.Revision == fileInfo.Revision)).ToList();

                if (notInDbSet.Count == 0)
                {
                    MessageBox.Show(@"Відсутні файли для перетворення.", @"Інформація про процес перетворенню файлів.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ISldWorksInstManager instManage = new SldWorksInstManager();
                _swApp = instManage.GetNewInstance();
                _swApp.Visible = true;

                IAssemblyDocument model = new AssemblyDocument(_swApp);

                var convert = new ConvertBuilder(_swApp);

                foreach (var file in notInDbSet)
                {
                    if (!GetFileCopy(file.FilePath)) continue;

                    var fileType = Path.GetExtension(file.FilePath);

                    switch (fileType)
                    {
                        case ".SLDDRW":
                        {
                            model.OpenFile(file.FilePath);
                            convert.PathBuilder(file.FilePath, "PDF", null, AppDataTemp());
                            convert.ConvertToPdf();
                            _swApp.CloseDoc(file.FilePath);
                            var pdfPath = ConvertBuilder.FilePath;
                            var blob = ReadAllBytes(pdfPath);

                            var newItem = new Items
                            {
                                DocumentId = file.DocumentId,
                                FileName = Path.GetFileName(pdfPath),
                                Revision = file.Revision,
                                Version = file.CurrentVersion,
                                DocType = "PDF",
                                Blob = blob
                            };

                            _db.Items.Add(newItem);
                            _db.SaveChanges();

                            if (pdfPath == null) continue;
                            if (!File.Exists(pdfPath)) continue;
                            Logger.Trace($"File.Delete {pdfPath}");
                            File.Delete(pdfPath);
                            break;
                        }
                        case ".SLDPRT":
                        {
                            model.OpenFile(file.FilePath);

                            if (!convert.IsSheetMetalComponent()) return;

                            var vConfig = model.GetDerivedConfig();
                            model.SuppressUpdates(false);

                            foreach (var config in vConfig)
                            {
                                convert.PathBuilder(file.FilePath, "DXF", config, AppDataTemp());
                                convert.ConvertToDxf(true);

                                var dxfPath = ConvertBuilder.FilePath;
                                var blob = ReadAllBytes(dxfPath);
                                Logger.Trace($"dxfPath {dxfPath}");

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

                                _db.Items.Add(newItem);
                                _db.SaveChanges();

                                if (dxfPath == null) continue;
                                if (!File.Exists(dxfPath)) continue;
                                Logger.Trace($"File.Delete {dxfPath}");
                                File.Delete(dxfPath);
                            }

                            break;
                        }
                    }

                    _swApp.CloseDoc(file.FilePath);
                }

                _swApp.CloseAllDocuments(true);
                _swApp.ExitApp();

                instManage.ReleaseInstance(_swApp);

                MessageBox.Show($@"Створено {notInDbSet.Count} файлів.", @"Звіт по перетворенню файлів.",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Нам дуже прикро, але в програмі сталася помилка.\nДля вирішення питання, зв'яжіться з техпідтримкою.\n{ex.Message}",
                    @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error(ex.Message);
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
            {
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
            }
            else
            {
                Logger.Error(@"File does not exist.");
            }

            return fileBytes;
        }

        public void ListFiles(string filePath)
        {
            _files = new List<FileInfo>();

            GetReferencedFiles(null, filePath, 0, "", ref _files);

            _files = (from record in _files
                group record by new
                {
                    record.DocumentId,
                    Filepath = record.FilePath,
                    record.FileName,
                    CurrentRevision = record.Revision,
                    record.CurrentVersion,
                    FolderID = record.FolderId,
                    //record.DrawPath
                }
                into grouping
                orderby grouping.Key.Filepath
                select new FileInfo
                {
                    DocumentId = grouping.Key.DocumentId,
                    FilePath = grouping.Key.Filepath,
                    FileName = grouping.Key.FileName,
                    Revision = grouping.Key.CurrentRevision,
                    CurrentVersion = grouping.Key.CurrentVersion,
                    FolderId = grouping.Key.FolderID,
                    //DrawPath = grouping.Key.DrawPath
                }).ToList();
        }

        public void GetReferencedFiles(IEdmReference10 reference, string filePath, int level, string projectName,
            ref List<FileInfo> fileList)
        {
            const bool top = false;

            if (reference == null)
            {
                //This is the first time this function is called for this 
                //reference tree; i.e., this is the root
                //Add the top-level file path to the dictionary

                var file = _vault.GetFileFromPath(filePath, out var parentFolder);

                if (file.IsKindOf(EdmObjectType.EdmObject_File))
                {
                    _localPath = file.GetLocalPath(parentFolder.ID);

                    fileList.Add(new FileInfo
                    {
                        DocumentId = file.ID,
                        FilePath = _localPath,
                        FileName = file.Name,
                        Revision = file.CurrentRevision,
                        CurrentVersion = file.CurrentVersion,
                        FolderId = parentFolder.ID
                    });

                    var drawingFile = CheckExistDrawingFile(_localPath, out var drawFolder);

                    if (drawingFile != null)
                    {
                        fileList.Add(new FileInfo
                        {
                            DocumentId = drawingFile.ID,
                            FilePath = drawingFile.GetLocalPath(drawFolder.ID),
                            FileName = drawingFile.Name,
                            Revision = drawingFile.CurrentRevision,
                            CurrentVersion = drawingFile.CurrentVersion,
                            FolderId = drawFolder.ID
                        });
                    }
                }

                //Get the reference tree for this file
                reference = (IEdmReference10)file.GetReferenceTree(parentFolder.ID);

                GetReferencedFiles(reference, "", level + 1, projectName, ref fileList);
            }
            else
            {
                //Execute this code when this function is called recursively; 
                //i.e., this is not the top-level IEdmReference in the tree

                //Recursively traverse the references
                var pos = reference.GetFirstChildPosition3(projectName, top, false, (int)EdmRefFlags.EdmRef_File,
                    "");
                while (!pos.IsNull)
                {
                    var @ref = (IEdmReference10)reference.GetNextChild(pos);


                    if (@ref.File.ObjectType == EdmObjectType.EdmObject_File)
                    {
                        if (@ref.FolderID == 0)
                        {
                            Logger.Warning(
                                $"ObjectType {(int)@ref.File.ObjectType} @ref.FolderID {@ref.FolderID} file.Name - {@ref.File.Name} {@ref.File.ID} {@ref.FoundPath}");
                        }

                        fileList.Add(new FileInfo
                        {
                            DocumentId = @ref.FileID,
                            FilePath = @ref.FoundPath,
                            FileName = @ref.Name,
                            Revision = @ref.File.CurrentRevision,
                            CurrentVersion = @ref.File.CurrentVersion,
                            FolderId = @ref.FolderID
                        });

                        var drawingFile = CheckExistDrawingFile(@ref.FoundPath, out var drawFolder);

                        if (drawingFile != null)
                        {
                            fileList.Add(new FileInfo
                            {
                                DocumentId = drawingFile.ID,
                                FilePath = drawingFile.GetLocalPath(drawFolder.ID),
                                FileName = drawingFile.Name,
                                Revision = drawingFile.CurrentRevision,
                                CurrentVersion = drawingFile.CurrentVersion,
                                FolderId = drawFolder.ID
                            });
                        }
                    }

                    GetReferencedFiles(@ref, "", level + 1, projectName, ref fileList);
                }
            }
        }

        public IEdmFile5 CheckExistDrawingFile(string path, out IEdmFolder5 drawFolder)
        {
            //Check if drawing file exist in the vault
            _drawPath = Regex.Replace(path.ToUpper(), "SLDASM|SLDPRT", "SLDDRW");
            var drawObj = _vault.GetFileFromPath(_drawPath, out drawFolder);

            return drawObj;
        }

        public bool GetFileCopy(string filePath, string version = "")
        {
            var fileName = string.Empty;
            try
            {
                var file = _vault.GetFileFromPath(filePath, out var parentFolder);
                file.GetFileCopy(0, version, parentFolder.ID,
                    (int)EdmGetFlag.EdmGet_Refs + (int)EdmGetFlag.EdmGet_Simple +
                    (int)EdmGetFlag.EdmGet_RefsVerLatest);
                fileName = file.Name;
                return true;
            }
            catch (COMException ex)
            {
                switch (ex.ErrorCode)
                {
                    case (int)EdmResultErrorCodes_e.E_EDM_INVALID_REVISION_NUMBER:
                        Logger.Error("Specified revision number is invalid.");
                        return false;
                    case (int)EdmResultErrorCodes_e.E_EDM_PERMISSION_DENIED:
                        Logger.Error(
                            $"The logged-in user lacks permission to see the specified version of the file. fileName - {fileName}");
                        return false;
                    case (int)EdmResultErrorCodes_e.E_EDM_FILE_NOT_FOUND:
                        Logger.Error(
                            $"The logged-in user lacks permission to see the specified version of the file. fileName - {fileName}");
                        return false;
                }

                return false;
            }
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
                $"Не налаштоване підключення до бази даних.\nЗверніться до вашого адміністратора для налаштування підключення.",
                @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}