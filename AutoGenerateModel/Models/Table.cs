using System.Collections.Generic;

namespace AutoGenerateModel.Models
{
    public class Table
    {
        /// <summary>
        /// 資料表名稱
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 資料表欄位
        /// </summary>
        public List<Column> Columns { get; set; }
    }
}
