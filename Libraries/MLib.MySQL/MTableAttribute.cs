using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLib.MySQL
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MTableAttribute : Attribute
    {
        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class MColumnAttribute : Attribute
    {
        /// <summary>
        /// 是否是主键
        /// </summary>
        public bool isPrimaryKey { get; set; } = false;

        /// <summary>
        /// 数据长度,如果填写9,2  那就是长度为9,两位小数的数值类型
        /// </summary>
        public string DataLength { get; set; } = string.Empty;

        /// <summary>
        /// 数据类型,varchar,int,blob
        /// </summary>
        public string DataType { get; set; } = "varchar";

        /// <summary>
        /// 默认值语句
        /// </summary>
        public string DefaultString { get; set; } = string.Empty;

        /// <summary>
        /// 是否为非null
        /// </summary>
        public bool IsNotNull { get; set; } = false;
    }
}
