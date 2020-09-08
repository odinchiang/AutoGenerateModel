using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AutoGenerateModel.Models;
using Table = AutoGenerateModel.Models.Table;

namespace AutoGenerateModel
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Dictionary<Type, string> _typeAliases = new Dictionary<Type, string>
        {
            { typeof(int), "int" },
            { typeof(short), "short" },
            { typeof(byte), "byte" },
            { typeof(byte[]), "byte[]" },
            { typeof(long), "long" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(float), "float" },
            { typeof(bool), "bool" },
            { typeof(string), "string" }
        };

        private readonly HashSet<Type> _nullableTypes = new HashSet<Type>
        {
            typeof(int),
            typeof(short),
            typeof(byte),
            typeof(long),
            typeof(double),
            typeof(decimal),
            typeof(float),
            typeof(bool),
            typeof(DateTime)
        };

        public string Server { get; set; }
        public string Uid { get; set; }
        public string Password { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// "Connect" Button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnConnect_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CheckServerInfo(TxtServer.Text, TxtUid.Text, TxtPassword.Password))
            {
                MessageBox.Show("Please fill in Server, UID and Password");
                return;
            }

            Server = TxtServer.Text;
            Uid = TxtUid.Text;
            Password = TxtPassword.Password;
            string connString = $"server={Server};database=master;uid={Uid};pwd={Password}";

            // 讀取該伺服器所有資料庫名稱(該帳號有存取權的)
            // HAS_DBACCESS() 如果使用者有資料庫的存取權，HAS_DBACCESS 會傳回 1；如果沒有存取權，則會傳回 0；如果資料庫名稱無效，則會傳回 NULL。
            // https://docs.microsoft.com/zh-tw/sql/t-sql/functions/has-dbaccess-transact-sql?view=sql-server-ver15
            string sql = @"SELECT name FROM master.dbo.sysdatabases
                           WHERE HAS_DBACCESS(name) = 1";

            List<string> dbs = new List<string>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        SqlDataReader dr = command.ExecuteReader();

                        while (dr.Read())
                        {
                            dbs.Add(dr["name"].ToString());
                        }
                    }
                }

                CbxDatabase.ItemsSource = dbs;
                CbxDatabase.SelectedIndex = 0;

                if (dbs.Count > 0)
                {
                    CbxDatabase.IsEnabled = true;
                    LbxTable.IsEnabled = true;
                    BtnGenerate.IsEnabled = true;
                    TxtServer.IsEnabled = false;
                    TxtUid.IsEnabled = false;
                    TxtPassword.IsEnabled = false;
                    BtnReset.IsEnabled = true;
                    BtnConnect.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// "Generate" 按鈕
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnGenerate_OnClick(object sender, RoutedEventArgs e)
        {
            string db = CbxDatabase.SelectedValue.ToString();
            string connString = $"server={Server};database={db};uid={Uid};pwd={Password}";
            string classNamespace = string.IsNullOrWhiteSpace(TxtNameSpace.Text) ? "Models" : TxtNameSpace.Text;
            List<Table> tables = new List<Table>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    using (SqlCommand command = new SqlCommand())
                    {
                        connection.Open();
                        command.Connection = connection;

                        // 取得 ListBox 選取的項目
                        foreach (ListBoxItem item in LbxTable.SelectedItems)
                        {
                            string tableName = item.Content.ToString()
                                .Split('.')[2]
                                .Replace("[", "")
                                .Replace("]", "");
                            command.CommandText = $"SELECT TOP 1 * FROM {item.Content}";
                            DataTable schema = new DataTable();

                            using (SqlDataReader dr = command.ExecuteReader())
                            {
                                // 獲取資料列的 Schema
                                schema = dr.GetSchemaTable();
                            }

                            List<Column> columns = new List<Column>();
                            foreach (DataRow row in schema.Rows)
                            {
                                Column column = new Column()
                                {
                                    Name = row["ColumnName"].ToString(),
                                    IsNullable = (bool)row["AllowDBNull"],
                                    DataType = (Type)row["DataType"]
                                };

                                columns.Add(column);
                            }

                            tables.Add(new Table()
                            {
                                Name = tableName,
                                Columns = columns
                            });
                        }
                    }
                }

                // 輸出至檔案
                string docPath = Path.Combine(Environment.CurrentDirectory, "AutoGenerateModels");
                if (!Directory.Exists(docPath))
                    Directory.CreateDirectory(docPath);

                foreach (Table table in tables)
                {
                    string text = GetModelText(table, classNamespace);
                    string fileName = $"{table.Name}.cs";

                    using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, fileName), false))
                    {
                        outputFile.WriteLine(text);
                    }
                }

                MessageBox.Show($"Output model file complete!\nThe file path is {docPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// "Reset" 按鈕
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {
            CbxDatabase.ItemsSource = null;
            LbxTable.Items.Clear();

            CbxDatabase.IsEnabled = false;
            LbxTable.IsEnabled = false;
            BtnGenerate.IsEnabled = false;
            TxtServer.IsEnabled = true;
            TxtUid.IsEnabled = true;
            TxtPassword.IsEnabled = true;
            BtnReset.IsEnabled = false;
            BtnConnect.IsEnabled = true;
        }

        /// <summary>
        /// 組合輸出檔案用的文字
        /// </summary>
        /// <param name="table">Table instance</param>
        /// <param name="classNamespace">name space</param>
        /// <returns></returns>
        private string GetModelText(Table table, string classNamespace)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("using System;");
            text.AppendLine("");
            text.AppendLine($"namespace {classNamespace}");
            text.AppendLine("{");
            text.AppendLine($"    public class {table.Name}");
            text.AppendLine("    {");

            foreach (Column column in table.Columns)
            {
                var type = column.DataType;
                var typeName = _typeAliases.ContainsKey(column.DataType) ? _typeAliases[type] : type.Name;
                var isNullable = column.IsNullable && _nullableTypes.Contains(type) ? "?" : "";
                var columnName = column.Name;

                text.AppendLine($"        public {typeName}{isNullable} {columnName} {{ get; set; }}");
            }

            text.AppendLine("    }");
            text.AppendLine("}");


            return text.ToString();
        }

        /// <summary>
        /// 檢查 Server, UID 及 Password 是否為空值
        /// </summary>
        /// <param name="server">Server</param>
        /// <param name="uid">UID</param>
        /// <param name="password">Password</param>
        /// <returns></returns>
        private bool CheckServerInfo(string server, string uid, string password)
        {
            return !string.IsNullOrWhiteSpace(server) &&
                   !string.IsNullOrWhiteSpace(uid) &&
                   !string.IsNullOrWhiteSpace(password);
        }

        /// <summary>
        /// CbxDatabase ComboBox selection changed event function
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbxDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(e.AddedItems.Count == 0) return;

            string db = e.AddedItems[0].ToString();
            string connString = $"server={Server};database={db};uid={Uid};pwd={Password}";

            string sql = @"SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME 
                           FROM INFORMATION_SCHEMA.Tables
                           WHERE TABLE_TYPE = 'BASE TABLE'
                           ORDER BY TABLE_NAME";
            List<string> tables = new List<string>();
            using (SqlConnection connection = new SqlConnection(connString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    SqlDataReader dr = command.ExecuteReader();

                    while (dr.Read())
                    {
                        tables.Add($"[{dr["TABLE_CATALOG"]}].[{dr["TABLE_SCHEMA"]}].[{dr["TABLE_NAME"]}]");
                    }
                }
            }

            // 將該資料庫所有資料表顯示於 ListBox
            LbxTable.Items.Clear();
            foreach (var item in tables)
            {
                ListBoxItem lbi = new ListBoxItem { Content = item };
                LbxTable.Items.Add(lbi);
            }
        }
    }
}
