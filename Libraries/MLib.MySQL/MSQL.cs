using MPlugin.Untruned.MPlugin.Core;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SDG.Unturned;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Mysqlx.Expect.Open.Types;

namespace MLib.MySQL
{
    public class MSQL
    {
        private MySqlConnection _connection;
        public MSQL(string address, string userName, string password, string databaseName, string port)
        {
            _connection = new MySqlConnection($"server={address};uid={userName};pwd={password};database={databaseName};port={port};charset=utf8");
        }

        /// <summary>
        /// 根据实体类初始化表,如果不存在则创建表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public async Task InitTableAsync<T>()
        {
            await InitTable<T>();
        }

        async Task InitTable<T>()
        {
            var type = typeof(T);
            var columnProperties = type.GetProperties();
            if (columnProperties != null)
            {
                var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
                string tableName = tableAttribute != null ? tableAttribute.TableName : type.Name;
                StringBuilder primaryKey = new StringBuilder("primary key(");
                StringBuilder columnInfo = new StringBuilder();
                string notnull = "";
                string defaultStr = "";
                foreach (var property in columnProperties)
                {
                    var attribute = property.GetCustomAttribute<MColumnAttribute>();
                    if (attribute != null)
                    {
                        if (attribute.isPrimaryKey)
                            primaryKey.Append($"`{property.Name}`,");
                        notnull = attribute.IsNotNull ? "not null" : "";
                        defaultStr = string.IsNullOrWhiteSpace(attribute.DefaultString) ? "" : $"default {attribute.DefaultString}";
                        if (string.IsNullOrWhiteSpace(attribute.DataLength))
                            columnInfo.Append($"`{property.Name}` {attribute.DataType} {notnull} {defaultStr},");
                        else
                            columnInfo.Append($"`{property.Name}` {attribute.DataType}({attribute.DataLength}) {notnull} {defaultStr},");
                    }
                    else
                    {
                        columnInfo.Append($"`{property.Name}` varchar(255),");
                    }
                }
                if (primaryKey.Length != "primary key(".Length)

                    primaryKey.Remove(primaryKey.Length - 1, 1).Append(")");
                else
                {
                    primaryKey.Remove(0, primaryKey.Length);
                    columnInfo.Remove(columnInfo.Length - 1, 1);
                }
                MySqlCommand command = new MySqlCommand
                {
                    Connection = _connection,
                    CommandText = $"create table if not exists `{tableName}` ({columnInfo}{primaryKey});"
                };
                await ExcuteCommand(command);
            }
            else throw new Exception("该类型不包含MColumn特性!");
        }


        /// <summary>
        /// 执行语句
        /// </summary>
        /// <param name="command"></param>
        /// <returns>返回执行是否成功</returns>
        public async Task<bool> ExcuteCommand(MySqlCommand command)
        {
            bool result = false;
            await _connection.OpenAsync();
            int row = await command.ExecuteNonQueryAsync();
            if (row > 0) result = true;
            await _connection.CloseAsync();
            return result;
        }






        /// <summary>
        /// 检测这个泛型实体类的主键是否已经存在
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> CheckPrimaryKeyExistsAsync<T>(T data)
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            var keyProperty = type.GetProperties().FirstOrDefault(p =>
            p.GetCustomAttribute<MColumnAttribute>() != null && p.GetCustomAttribute<MColumnAttribute>().isPrimaryKey);
            if (keyProperty == null)
                throw new Exception("Primary key not found.");
            string keyName = keyProperty.Name;
            var keyValue = keyProperty.GetValue(data);
            if (keyValue == null)
                throw new Exception("Primary key value cannot be empty.");
            string query = $"select `{keyName}` from `{tableName}` where `{keyName}` = @{keyName}";
            var parameter = new MySqlParameter($"@{keyName}", keyValue);
            MySqlCommand command = new MySqlCommand(query, _connection);
            command.Parameters.Add(parameter);
            await _connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            await _connection.CloseAsync();
            return result != null;
        }




        /// <summary>
        /// 检测这个泛型实体类的非空字段是否已经存在
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> CheckColumnValueExistsAsync<T>(T data)
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            // 获取所有的属性和列名
            var properties = type.GetProperties();
            var columnNames = properties.Select(p => p.Name).ToList();
            // 构建查询语句和参数列表
            var query = new StringBuilder($"select * from `{tableName}` where ");
            var parameters = new List<MySqlParameter>();
            bool hasCondition = false; // 是否有至少一个非空字段
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var columnName = columnNames[i];
                var value = property.GetValue(data);
                if (value != null) // 只考虑非空字段
                {
                    if (hasCondition) // 如果已经有条件，就加上 and
                    {
                        query.Append(" and ");
                    }
                    query.Append($"`{columnName}` = @{columnName}"); // 添加条件
                    parameters.Add(new MySqlParameter($"@{columnName}", value)); // 添加参数
                    hasCondition = true;
                }
            }
            if (!hasCondition) // 如果没有任何非空字段，就抛出异常
            {
                throw new Exception("No non-null fields found.");
            }
            // 执行查询并返回结果
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            await _connection.CloseAsync();
            return result != null;
        }












        /// <summary>
        /// 异步查询所有结果
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>返回所有结果</returns>
        public async Task<List<T>> QueryAsync<T>()
        {
            return await Query<T>();
        }
        async Task<List<T>> Query<T>()
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            string query = $"select * from `{tableName}`";
            MySqlCommand command = new MySqlCommand(query, _connection);
            await _connection.OpenAsync();
            MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            List<T> list = new List<T>();
            while (await reader.ReadAsync())
            {
                T obj = (T)Activator.CreateInstance(type);
                foreach (var prop in type.GetProperties())
                {
                    string columnName = prop.Name;
                    var value = reader[columnName];
                    if (value != DBNull.Value)
                        prop.SetValue(obj, JsonConvert.DeserializeObject(value.ToString(), prop.PropertyType));
                }
                list.Add(obj);
            }
            await _connection.CloseAsync();
            return list;
        }



        /// <summary>
        /// 按条件查询,只按照参数里不为空的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="condition"></param>
        /// <returns></returns>
        public async Task<List<T>> QueryAsync<T>(T data)
        {
            return await Query<T>(data);
        }
        async Task<List<T>> Query<T>(T data)
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"select * from `{tableName}`");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            foreach (var prop in type.GetProperties())
            {
                var value = prop.GetValue(data);
                if (value != null)
                {
                    string columnName = prop.Name;
                    query.Append(query.Length == $"select * from `{tableName}`".Length ? " where " : " and ");
                    query.Append($"{columnName} = @{columnName}");
                    MySqlParameter parameter = new MySqlParameter($"@{columnName}", value);
                    parameters.Add(parameter);
                }
            }
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            List<T> list = new List<T>();
            while (await reader.ReadAsync())
            {
                T obj = (T)Activator.CreateInstance(type);
                foreach (var prop in type.GetProperties())
                {
                    string columnName = prop.Name;
                    var value = reader[columnName];
                    if (value != DBNull.Value)
                    {
                        var formatter = new BinaryFormatter();
                        using (var ms = new MemoryStream((byte[])value))
                        {
                            prop.SetValue(obj, formatter.Deserialize(ms));
                        }
                    }

                }
                list.Add(obj);
            }
            await _connection.CloseAsync();
            return list;
        }

        public enum EQueryOperator
        {
            between,
            bigMore,
            equal,
            smallMore,
            smallAndEqual,
            bigAndEqual
        }

        public class QueryCondition
        {
            /// <summary>
            /// 字段名
            /// </summary>
            public string ColumnName { get; set; }

            /// <summary>
            /// 操作符，如=, >, <, >=, <=, between
            /// </summary>
            public EQueryOperator Operator { get; set; }

            /// <summary>
            /// 值，可以是单个值或者一个数组，根据操作符的不同而变化
            /// </summary>
            public object Value { get; set; }
        }


        /// <summary>
        /// 按条件查询,大于小于等于及两数之间
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="condition"></param>
        /// <returns></returns>
        public async Task<List<T>> QueryAsync<T>(List<QueryCondition> conditions)
        {
            return await Query<T>(conditions);
        }
        async Task<List<T>> Query<T>(List<QueryCondition> conditions)
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"select * from `{tableName}`");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                query.Append(query.Length == $"select * from `{tableName}`".Length ? " where " : " and ");
                query.Append($"{condition.ColumnName} ");
                switch (condition.Operator)
                {
                    case EQueryOperator.between:
                        query.Append($"BETWEEN @{condition.ColumnName}{i}a AND @{condition.ColumnName}{i}b");
                        var values = (object[])condition.Value;
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}a", values[0]));
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}b", values[1]));
                        break;
                    case EQueryOperator.bigMore:
                        query.Append($"> @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.equal:
                        query.Append($"= @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.smallMore:
                        query.Append($"< @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.smallAndEqual:
                        query.Append($"<= @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.bigAndEqual:
                        query.Append($">= @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    default:
                        throw new Exception("Invalid operator");
                }
            }
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            List<T> list = new List<T>();
            while (await reader.ReadAsync())
            {
                T obj = (T)Activator.CreateInstance(type);
                foreach (var prop in type.GetProperties())
                {
                    string columnName = prop.Name;
                    var value = reader[columnName];
                    if (value != DBNull.Value)
                        prop.SetValue(obj, JsonConvert.DeserializeObject(value.ToString(), prop.PropertyType));
                    //prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                }
                list.Add(obj);
            }
            await _connection.CloseAsync();
            return list;
        }




        public enum EQueryOrderByOperator
        {
            /// <summary>
            /// 升序
            /// </summary>
            ASC,
            /// <summary>
            /// 降序
            /// </summary>
            DESC
        }
        public class QueryOrderByCondition
        {
            /// <summary>
            /// 排序类型,升序or降序
            /// </summary>
            public EQueryOrderByOperator EQueryOrderByOperator { get; set; }

            /// <summary>
            /// 字段名称
            /// </summary>
            public string ColumnName { get; set; }
        }

        /// <summary>
        /// 按条件查询,降序or升序的n行数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conditions"></param>
        /// <param name="rowCount"></param>
        /// <returns></returns>
        public async Task<List<T>> QueryAsync<T>(List<QueryOrderByCondition> conditions, int rowCount)
        {
            return await Query<T>(conditions, rowCount);
        }
        async Task<List<T>> Query<T>(List<QueryOrderByCondition> conditions, int rowCount)
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"select * from `{tableName}`");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                query.Append(query.Length == $"select * from `{tableName}`".Length ? " order by " : " , ");
                query.Append($"{condition.ColumnName} {condition.EQueryOrderByOperator} ");
            }
            query.Append($" limit {rowCount}");
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            List<T> list = new List<T>();
            while (await reader.ReadAsync())
            {
                T obj = (T)Activator.CreateInstance(type);
                foreach (var prop in type.GetProperties())
                {
                    string columnName = prop.Name;
                    var value = reader[columnName];
                    if (value != DBNull.Value)
                        prop.SetValue(obj, JsonConvert.DeserializeObject(value.ToString(), prop.PropertyType));
                    //prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                }
                list.Add(obj);
            }
            await _connection.CloseAsync();
            return list;
        }



        /// <summary>
        /// 添加数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> AddDataAsync<T>(T data)
        {
            return await AddData<T>(data);
        }
        async Task<bool> AddData<T>(T data)
        {
            if (await CheckPrimaryKeyExistsAsync(data)) return false;
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"insert into `{tableName}` (");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            foreach (var prop in type.GetProperties())
            {
                string columnName = prop.Name;
                query.Append($"{columnName},");
                var value = prop.GetValue(data);
                if (value is ICollection) // 如果属性是集合
                {
                    var formatter = new BinaryFormatter();
                    using (var ms = new MemoryStream())
                    {
                        formatter.Serialize(ms, value);
                        parameters.Add(new MySqlParameter($"@{columnName}", ms.ToArray()));
                    }
                }
                else
                {
                    parameters.Add(new MySqlParameter($"@{columnName}", value));
                }
            }

            query.Remove(query.Length - 1, 1); //移除最后一个逗号
            query.Append(") values (");
            foreach (var param in parameters)
            {
                query.Append($"{param.ParameterName},");
            }
            query.Remove(query.Length - 1, 1); //移除最后一个逗号
            query.Append(")");
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            int result = await command.ExecuteNonQueryAsync();
            await _connection.CloseAsync();
            return result > 0;
        }



        /// <summary>
        /// 添加数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> AddDataAsync<T>(List<T> data)
        {
            return await AddData<T>(data);
        }
        async Task<bool> AddData<T>(List<T> datas)
        {
            bool successful = true;
            for (int i = 0; i < datas.Count; i++)
            {
                successful = await AddData(datas[i]);
                if (!successful) return false;
            }
            return successful;
        }






        /// <summary>
        /// 删除数据的方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conditions"></param>
        /// <returns></returns>
        public async Task<bool> DeleteDataAsync<T>(List<QueryCondition> conditions)
        {
            return await DeleteData<T>(conditions);
        }
        async Task<bool> DeleteData<T>(List<QueryCondition> conditions)
        {
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"delete from `{tableName}`");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                query.Append(i == 0 ? " where " : " and ");
                query.Append($"{condition.ColumnName} ");
                switch (condition.Operator)
                {
                    case EQueryOperator.between:
                        query.Append($"BETWEEN @{condition.ColumnName}{i}a AND @{condition.ColumnName}{i}b");
                        var values = (object[])condition.Value;
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}a", values[0]));
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}b", values[1]));
                        break;
                    case EQueryOperator.bigMore:
                        query.Append($"> @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.equal:
                        query.Append($"= @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.smallMore:
                        query.Append($"< @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.smallAndEqual:
                        query.Append($"<= @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    case EQueryOperator.bigAndEqual:
                        query.Append($">= @{condition.ColumnName}{i}");
                        parameters.Add(new MySqlParameter($"@{condition.ColumnName}{i}", condition.Value));
                        break;
                    default:
                        throw new Exception("Invalid operator");
                }
            }
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            int result = await command.ExecuteNonQueryAsync();
            await _connection.CloseAsync();
            return result > 0;
        }


        /// <summary>
        /// 删除数据的方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<bool> DeleteDataAsync<T>(T data)
        {
            return await DeleteData<T>(data);
        }
        async Task<bool> DeleteData<T>(T data)
        {
            if (!await CheckPrimaryKeyExistsAsync(data)) return false;
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"delete from `{tableName}`");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            query.Append(query.Length == $"delete from `{tableName}`".Length ? " where " : " and ");
            string keyColumnName = "";
            string keyColumnValue = "";
            foreach (var prop in type.GetProperties())
            {
                if (prop.GetCustomAttribute<MColumnAttribute>() != null && prop.GetCustomAttribute<MColumnAttribute>().isPrimaryKey)
                {
                    keyColumnName = prop.Name;
                    keyColumnValue = prop.GetValue(data).ToString();
                }
            }
            query.Append($" {keyColumnName}={keyColumnValue} ");
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            int result = await command.ExecuteNonQueryAsync();
            await _connection.CloseAsync();
            return result > 0;
        }



        /// <summary>
        /// 更新数据的方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conditions"></param>
        /// <returns></returns>
        public async Task<bool> UpdateDataAsync<T>(T data)
        {
            return await UpdateData<T>(data);
        }
        async Task<bool> UpdateData<T>(T data)
        {
            if (!await CheckPrimaryKeyExistsAsync(data)) return false;
            var type = typeof(T);
            var tableAttribute = type.GetCustomAttribute<MTableAttribute>();
            string tableName = type.Name;
            if (tableAttribute != null)
                tableName = tableAttribute.TableName;
            StringBuilder query = new StringBuilder($"update `{tableName}` set ");
            List<MySqlParameter> parameters = new List<MySqlParameter>();
            string keyColumnName = "";
            string keyColumnValue = "";
            foreach (var prop in type.GetProperties())
            {
                if (prop.GetCustomAttribute<MColumnAttribute>() != null && prop.GetCustomAttribute<MColumnAttribute>().isPrimaryKey)
                {
                    keyColumnName = prop.Name;
                    keyColumnValue = prop.GetValue(data).ToString();
                }
                else
                {
                    string columnName = prop.Name;
                    query.Append($"{columnName} = @{columnName},");
                    parameters.Add(new MySqlParameter($"@{columnName}", prop.GetValue(data)));
                }
            }
            query.Remove(query.Length - 1, 1);
            query.Append($" where {keyColumnName}={keyColumnValue}");
            MySqlCommand command = new MySqlCommand(query.ToString(), _connection);
            command.Parameters.AddRange(parameters.ToArray());
            await _connection.OpenAsync();
            int result = await command.ExecuteNonQueryAsync();
            await _connection.CloseAsync();
            return result > 0;
        }




        /// <summary>
        /// 更新数据的方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conditions"></param>
        /// <returns></returns>
        public async Task<bool> UpdateDataAsync<T>(List<T> datas)
        {
            return await UpdateData<T>(datas);
        }
        async Task<bool> UpdateData<T>(List<T> datas)
        {
            bool successful = true;
            for (int i = 0; i < datas.Count; i++)
            {
                successful = await UpdateData(datas[i]);
            }
            return successful;
        }
    }
}
