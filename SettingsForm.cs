using CADShark.Common.DBManager.Data;
using CADShark.Common.Logging;
using EPDM.Interop.epdm;
using System;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace CADShark.OpenBatchPDM.AddIn
{
    public partial class SettingsForm : Form
    {
        private readonly IEdmVault5 _vault;
        private IEdmDictionary5 _projectDictionary;
        private const string DictionaryName = "OpenBatch";
        private string _connectionString;
        private string _action;
        private static readonly CadLogger Logger = CadLogger.GetLogger(className: nameof(SettingsForm));

        public SettingsForm(IEdmVault5 edmVault5)
        {
            _vault = edmVault5;
            InitializeComponent();
        }
        
        private void SettingsForm_Load(object sender, EventArgs e)
        {
            _action = null;
            GetDictionaryValues();
            //FillCombo();
            ConnStringTextBox.Text = _connectionString;

        }

        private void CheckConnectButtonClick(object sender, EventArgs e)
        {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    MessageBox.Show(
                        $@"The connection string cannot be empty!",
                        @"Error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string dbName;
                try
                {
                    var builder = new SqlConnectionStringBuilder(_connectionString);
                    dbName = builder.InitialCatalog;
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(@"Error in connection string: " + ex.Message, @"An error occurred in the program.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var result = DatabaseInitializer.EnsureDatabaseAndTable(_connectionString, dbName);

                if (result.Success)
                {

                        MessageBox.Show(@"Connection successful!", @"Info", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        result.Error,
                        @"Error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

        }

        private void GetDictionaryValues()
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

        private void ConnStringTextBox_TextChanged(object sender, EventArgs e)
        {
            _connectionString = ConnStringTextBox.Text;
        }

        private void DbUpdateButton_Click(object sender, EventArgs e)
        {
           
        }

        private void FillCombo()
        {
            string[] arr = { "open", "view", "explore", "get", "lock", "properties", "history" };
            ActionComboBox.DataSource = arr;

            if (_action != null)
            {
                ActionComboBox.SelectedText = _action;
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
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
            //Add Action to Dictionary 
            _projectDictionary.StringSetAt("Action", _action);
        }

        private void CloseButton_Click_1(object sender, EventArgs e)
        {
            Close();
        }
    }
}