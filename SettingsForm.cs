using CADShark.Common.Analytics;
using CADShark.Common.DBManager.Data;
using CADShark.Common.Feedback;
using CADShark.Common.Logging;
using EPDM.Interop.epdm;
using System;
using System.Windows.Forms;

namespace CADShark.OpenBatchPDM.Addin
{
    public partial class SettingsForm : Form
    {
        private readonly IEdmVault5 _vault;
        private IEdmDictionary5 _projectDictionary;
        private IAddDbContext _db;
        private const string DictionaryName = "OpenBatch";
        private string _connectionString;
        private static readonly CadLogger Logger = CadLogger.GetLogger(className: nameof(SettingsForm));

        public SettingsForm(IEdmVault5 edmVault5)
        {
            _vault = edmVault5;
            InitializeComponent();
        }

        public SettingsForm()
        {
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            GetConnString();
            ConnStringTextBox.Text = _connectionString;
        }

        private void CheckConnectButtonClick(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    MessageBox.Show(
                        $@"Рядок підключення не може бути пустим!",
                        @"Попередження.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _db = new AddDbContext(_connectionString);

                _db.CreateDataBase(error: out var error);

                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show(
                        $"Нам дуже прикро, але в програмі сталася помилка.Для вирішення питання, зв'яжіться з техпідтримкою.\n{error}",
                        @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                else
                {
                    //Get the selected dictionary, if it exists
                    _projectDictionary = _vault.GetDictionary(DictionaryName, false);

                    //If it doesn't exist, create it
                    if (_projectDictionary == null)
                    {
                        //Create it, because it doesn't exist
                        _projectDictionary = _vault.GetDictionary(DictionaryName, true);
                    }

                    //Add Connection String to Dictionary 
                    _projectDictionary.StringSetAt("ConnectionString", _connectionString);


                    MessageBox.Show(
                        $@"Підключення до бази даних пройшло успішно.",
                        @"Підключення до бази даних.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"Нам дуже прикро, але в програмі сталася помилка.Для вирішення питання, зв'яжіться з техпідтримкою.\n{exception.Message}",
                    @"Виникла помилка у роботі програми.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Error("Error connecting to  MS SQL Server", exception);
                throw;
            }

        }

        private void GetConnString()
        {
            _projectDictionary = _vault.GetDictionary(DictionaryName, false);
            if (_projectDictionary == null) return;

            var pos = _projectDictionary.StringFindKeys("ConnectionString");

            while (!pos.IsNull)
            {
                _projectDictionary.StringGetNextAssoc(pos, out _, out var value);
                _connectionString = value;
            }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void InfoButton_Click(object sender, EventArgs e)
        {
            new InfoForm().ShowDialog();
            Telemetry.LogButtonClick("Show Info Form.");
        }

        private void FeedBackButton_Click(object sender, EventArgs e)
        {
            _ = new FeedbackWindow().ShowDialog();
            Telemetry.LogButtonClick("Show Feedback Window.");
        }

        private void ConnStringTextBox_TextChanged(object sender, EventArgs e)
        {
            _connectionString = ConnStringTextBox.Text;
        }

        private void DbUpdatebutton_Click(object sender, EventArgs e)
        {
            _db.Migrator();
            Telemetry.LogButtonClick("Put button Update DataBase.");
        }
    }
}