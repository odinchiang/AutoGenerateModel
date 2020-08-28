using System;

namespace AutoGenerateModel.Models
{
    public class Column
    {
        /// <summary>
        /// 欄位名稱
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 欄位是否允許 Null
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// 欄位資料類型
        /// </summary>
        public Type DataType { get; set; }
    }
}
