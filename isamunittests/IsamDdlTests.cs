//-----------------------------------------------------------------------
// <copyright file="IsamDdlTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

namespace IsamUnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using Microsoft.Database.Isam;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Rhino.Mocks;

    using Any = InteropApiTests.Any;
    using Miei = Microsoft.Isam.Esent.Interop;

    /// <summary>
    /// Basic DDL tests
    /// </summary>
    [TestClass]
    public class IsamDdlTests
    {
        /// <summary>
        /// The name of one of the strings add
        /// </summary>
        private const string TestColumnName = "StringTestColumn";

        /// <summary>
        /// The directory being used for the database and its files.
        /// </summary>
        private string directory;

        /// <summary>
        /// The path to the database being used by the test.
        /// </summary>
        private string databaseName;

        /// <summary>
        /// The name of the table.
        /// </summary>
        private string tableName;

        /// <summary>
        /// The instance used by the test.
        /// </summary>
        private IsamInstance instance;

        /// <summary>
        /// The session used by the test.
        /// </summary>
        private IsamSession sesid;

        /// <summary>
        /// Identifies the database used by the test.
        /// </summary>
        private IsamDatabase dbid;

        /// <summary>
        /// The tableid being used by the test.
        /// </summary>
        private Cursor tableid;

        /// <summary>
        /// Columnid of the column in the table.
        /// </summary>
        private ColumnDefinition testColumnid;

        #region Setup/Teardown

        /// <summary>
        /// Initialization method. Called once when the tests are started.
        /// </summary>
        [TestInitialize]
        [Description("Setup for IsamDdlTests")]
        public void Setup()
        {
            this.directory = SetupHelper.CreateRandomDirectory();
            this.databaseName = Path.Combine(this.directory, "database.edb");
            this.tableName = "table";
            this.instance = SetupHelper.CreateNewInstance(this.directory);

            IsamSystemParameters isamSystemParameters = this.instance.IsamSystemParameters;
            isamSystemParameters.Recovery = "off";

            this.sesid = this.instance.CreateSession();

            this.sesid.CreateDatabase(this.databaseName);
            this.sesid.AttachDatabase(this.databaseName);

            this.dbid = this.sesid.OpenDatabase(this.databaseName);

            using (IsamTransaction transaction = new IsamTransaction(this.sesid))
            {
                var tabledef = new TableDefinition(this.tableName);
                this.testColumnid = new ColumnDefinition(TestColumnName) { Type = typeof(string), };

                tabledef.Columns.Add(this.testColumnid);
                tabledef.Columns.Add(new ColumnDefinition("TestColumn1") { Type = typeof(int), });
                tabledef.Columns.Add(new ColumnDefinition("TestColumn2") { Type = typeof(int), });

                var primaryIndex = new IndexDefinition("PrimaryIndex")
                                       {
                                           KeyColumns =
                                               {
                                                   new KeyColumn(
                                                       "TestColumn1",
                                                       true),
                                               },
                                       };
                primaryIndex.Flags = IndexFlags.Primary;
                tabledef.Indices.Add(primaryIndex);

                var textIndex = new IndexDefinition("TextIndex")
                                    {
                                        KeyColumns =
                                            {
                                                new KeyColumn(
                                                    "StringTestColumn",
                                                    true),
                                            },
                                        CultureInfo = new CultureInfo("en-CA"),
                                    };
                tabledef.Indices.Add(textIndex);

                this.dbid.CreateTable(tabledef);

                transaction.Commit();
            }

            this.tableid = this.dbid.OpenCursor(this.tableName);
        }

        /// <summary>
        /// Cleanup after all tests have run.
        /// </summary>
        [TestCleanup]
        [Description("Cleanup for IsamDdlTests")]
        public void Teardown()
        {
            this.tableid.Dispose();
            this.dbid.Dispose();
            this.sesid.Dispose();
            this.instance.Dispose();
            //// Cleanup.DeleteDirectoryWithRetry(this.directory);
        }

        /// <summary>
        /// Verify that IsamDdlTests has setup the test fixture properly.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify that IsamDcursor.csdlTests has setup the test fixture properly")]
        public void VerifyFixtureSetup()
        {
            Assert.IsNotNull(this.tableName);
            Assert.IsNotNull(this.instance);
            Assert.IsNotNull(this.sesid);
            Assert.IsNotNull(this.dbid);
            Assert.IsNotNull(this.tableid);
            Assert.IsNotNull(this.testColumnid);

            // MAC_TODO: Shouldn't this be non-null?
            Assert.IsNull(this.testColumnid.Columnid);

            // MAC_TODO: metadata verification:
            // JET_COLUMNDEF columndef;
            // Api.JetGetTableColumnInfo(this.sesid, this.tableid, this.testColumnid, out columndef);
            // Assert.AreEqual(JET_coltyp.LongText, columndef.coltyp);
        }

        #endregion Setup/Teardown

        #region DDL Tests

        /// <summary>
        /// Create one column of each type.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Create one column of each type")]
        public void CreateOneColumnOfEachType()
        {
            Type[] validTypes = new Type[]
                                    {
                                        typeof(bool), typeof(byte), typeof(char), typeof(System.DateTime), typeof(double),
                                        typeof(short), typeof(int), typeof(long), typeof(float), typeof(string),
                                        typeof(ushort), typeof(uint), typeof(byte[]), typeof(System.Guid),
                                    };
            using (var trx = new IsamTransaction(this.sesid))
            {
                foreach (var coltyp in validTypes)
                {
                    var columnName = coltyp.ToString();
                    columnName = columnName.Substring(columnName.LastIndexOf('.') + 1);

                    // Special case byte[]
                    if ("Byte[]" == columnName)
                    {
                        columnName = "ByteArray";
                    }

                    Debug.Print("columnName is '{0}'", columnName);

                    var columndef = new ColumnDefinition(columnName) { Type = coltyp };
                    this.tableid.TableDefinition.AddColumn(columndef);

                    Assert.AreEqual(columndef.Name, this.tableid.TableDefinition.Columns[columnName].Name);
                }

                trx.Commit(false);
            }
        }

        /// <summary>
        /// Create a column with a default value.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Create a column with a default value")]
        public void CreateColumnWithDefaultValue()
        {
            int defaultValue = Any.Int32;
            const string NewColumnName = "column_with_default";

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                var columndef = new ColumnDefinition(NewColumnName) { Type = typeof(int), DefaultValue = defaultValue, };

                this.tableid.TableDefinition.AddColumn(columndef);

                trx.Commit(false);

                try
                {
                    using (IsamTransaction trx2 = new IsamTransaction(this.sesid))
                    {
                        using (var cursor = this.dbid.OpenCursor(this.tableName))
                        {
                            cursor.BeginEditForInsert();
                            cursor.AcceptChanges();
                            trx2.Commit(false);

                            cursor.MoveBeforeFirst();
                            cursor.MoveNext();
                            Assert.AreEqual(defaultValue, cursor.Record[NewColumnName]);
                        }
                    }
                }
                finally
                {
                    this.tableid.TableDefinition.DropColumn(NewColumnName);
                }
            }
        }

        /// <summary>
        /// Insert a single record.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Simple Insertion.")]
        public void SimpleInsertion()
        {
            var tableDefinition = new TableDefinition("test4table");
            tableDefinition.Columns.Add(new ColumnDefinition("col1") { Type = typeof(int), });
            tableDefinition.Columns.Add(new ColumnDefinition("col2") { Type = typeof(int) });

            var idx = new IndexDefinition("idx1");
            idx.KeyColumns.Add(new KeyColumn("col1", true));
            tableDefinition.Indices.Add(idx);
            this.dbid.CreateTable(tableDefinition);

            using (var cursor = this.dbid.OpenCursor("test4table"))
            {
                cursor.BeginEditForInsert();
                cursor.EditRecord["col1"] = 1;

                // here it goes key not found when it tries to retrieve columndefinition from the db
                cursor.EditRecord["col2"] = 2;
                cursor.AcceptChanges();
            }

            this.dbid.DropTable("test4table");
        }

        /// <summary>
        /// Insert a single record with emtpy string.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Simple Insertion of empty string.")]
        public void SimpleInsertionEmptyString()
        {
            string tableName = "test4insertionStringEmpty";
            var tableDefinition = new TableDefinition(tableName);
            tableDefinition.Columns.Add(new ColumnDefinition("col1") { Type = typeof(string) });

            this.dbid.CreateTable(tableDefinition);

            using (var cursor = this.dbid.OpenCursor(tableName))
            {
                cursor.BeginEditForInsert();
                cursor.EditRecord["col1"] = string.Empty;
                cursor.AcceptChanges();

                cursor.MoveBeforeFirst();
                cursor.MoveNext();
                Assert.AreEqual(string.Empty, cursor.Record["col1"]);
            }

            this.dbid.DropTable(tableName);
        }

        /// <summary>
        /// FindRecords(), then MoveNext().
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("FindRecords(), then MoveNext().")]
        public void SimpleInsertionFindRecordsMoveNext()
        {
            string tableName = "test4tableMoveNext";
            var tableDefinition = new TableDefinition(tableName);
            tableDefinition.Columns.Add(new ColumnDefinition("col1") { Type = typeof(int) });
            tableDefinition.Columns.Add(new ColumnDefinition("col2") { Type = typeof(int) });
            var idx = new IndexDefinition("primary");
            idx.KeyColumns.Add(new KeyColumn("col1", true) { });
            tableDefinition.Indices.Add(idx);
            this.dbid.CreateTable(tableDefinition);

            using (var cursor = this.dbid.OpenCursor(tableName))
            {
                for (int i = 3; i < 50; i++)
                {
                    cursor.BeginEditForInsert();
                    cursor.EditRecord["col1"] = i;
                    cursor.EditRecord["col2"] = i;
                    cursor.AcceptChanges();
                }

                cursor.SetCurrentIndex("primary");

                for (int i = 7; i < 50; i++)
                {
                    Console.WriteLine();
                    Console.WriteLine("Key: {0}", i);

                    var searchKey = Key.Compose(i);

                    // cursor.MoveBeforeFirst();
                    // this one places itself before the seek found key, but...
                    cursor.FindRecords(MatchCriteria.EqualTo, searchKey);

                    // the call to .MoveNext() loses it and goes to the first record in the index(not the one filtered)
                    foreach (FieldCollection record in cursor)
                    {
                        Console.WriteLine("\tName={0}, Count = {1}.", record.Names, record.Count);
                        foreach (FieldValueCollection fieldItem in record)
                        {
                            Console.WriteLine(
                                "\tName={0}, Count = {1}, Value={2}.",
                                fieldItem.Name,
                                fieldItem.Count,
                                fieldItem[0]);

                            var dataKey = fieldItem[0];
                            if (dataKey.Equals(i))
                            {
                                Console.WriteLine("ok");
                            }

                            Assert.AreEqual(i, dataKey);
                        }
                    }
                }
            }

            this.dbid.DropTable(tableName);
        }

        /// <summary>
        /// Add a column and retrieve its information using ColumnCollection[Name].
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Add a column and retrieve its information using ColumnCollection[Name]")]
        public void TestColumnCollectionByName()
        {
            const string ColumnName = "column1";
            using (var trx = new IsamTransaction(this.sesid))
            {
                var columndefExpected = new ColumnDefinition(ColumnName)
                                            {
                                                MaxLength = 4096,
                                                Type = typeof(string),
                                                Flags = ColumnFlags.None,
                                            };

                this.tableid.TableDefinition.AddColumn(columndefExpected);
                trx.Commit(false);

                ColumnDefinition retrievedColumndef = this.tableid.TableDefinition.Columns[ColumnName];

                Assert.AreEqual(columndefExpected.MaxLength, retrievedColumndef.MaxLength);
                Assert.AreEqual(false, retrievedColumndef.IsAscii);
                Assert.AreEqual(columndefExpected.Type, retrievedColumndef.Type);
                Assert.IsNotNull(retrievedColumndef.Columnid);

                // The Flags comparison fails, as esent will add some options by default
                // Assert.AreEqual(columndefExpected.Flags, retrievedColumndef.Flags);
                Assert.AreEqual(ColumnFlags.Sparse, retrievedColumndef.Flags);
            }
        }

        /// <summary>
        /// Add a column and retrieve its information using ColumnCollection[ColumnId].
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Add a column and retrieve its information using ColumnCollection[ColumnId]")]
        public void TestColumnCollectionByColumnid()
        {
            const string ColumnName = "column2";
            using (var trx = new IsamTransaction(this.sesid))
            {
                var columndefExpected = new ColumnDefinition(ColumnName)
                                            {
                                                MaxLength = 8192,
                                                Type = typeof(string),
                                                Flags = ColumnFlags.None,
                                            };

                this.tableid.TableDefinition.AddColumn(columndefExpected);
                trx.Commit(false);

                ColumnDefinition columndefForColumnid = this.tableid.TableDefinition.Columns[ColumnName];
                ColumnDefinition retrievedColumndef =
                    this.tableid.TableDefinition.Columns[columndefForColumnid.Columnid];

                Assert.AreEqual(columndefExpected.MaxLength, retrievedColumndef.MaxLength);
                Assert.AreEqual(false, retrievedColumndef.IsAscii);
                Assert.AreEqual(columndefExpected.Type, retrievedColumndef.Type);
                Assert.IsNotNull(retrievedColumndef.Columnid);

                // The Flags aren't asserted as esent will add some options by default
            }
        }

        /// <summary>
        /// Add a column and retrieve its information with JET_COLUMNBASE using JetGetTableColumnInfo, specifying column name.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Add a column and retrieve its information with JET_COLUMNBASE using JetGetTableColumnInfo, specifying column name.")]
        public void JetGetTableColumnBaseInfo()
        {
            const string ColumnName = "column1";
            var columndef = new ColumnDefinition(ColumnName, typeof(string), ColumnFlags.Variable);
            columndef.MaxLength = 4096;
            Assert.IsFalse(columndef.IsAscii);

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                this.tableid.TableDefinition.AddColumn(columndef);

                trx.Commit();
            }

            ColumnDefinition actual = this.tableid.TableDefinition.Columns[ColumnName];

            Assert.AreEqual(columndef.MaxLength, actual.MaxLength);
            Assert.AreEqual(ColumnFlags.Sparse, actual.Flags);
            Assert.AreEqual(columndef.IsAscii, actual.IsAscii);
            Assert.IsFalse(actual.IsAscii);
            //// Assert.AreEqual(columndef.Columnid, actual.Columnid);
            Assert.AreEqual(columndef.Type, actual.Type);

            // The output grbit isn't checked for equality as esent will add some options by default.
        }

        /// <summary>
        /// Check that the dictionary returned by GetColumnDictionary is case-insensitive.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Check that the dictionary returned by GetColumnDictionary is case-insensitive")]
        public void VerifyGetColumnDictionaryReturnsCaseInsensitiveDictionary()
        {
            ColumnCollection columnCollection = this.tableid.TableDefinition.Columns;
            ColumnDefinition actual = columnCollection[TestColumnName.ToUpper()];

            Assert.AreEqual(this.testColumnid.MaxLength, actual.MaxLength);
            Assert.AreEqual(ColumnFlags.Sparse, actual.Flags);
            Assert.AreEqual(this.testColumnid.IsAscii, actual.IsAscii);
            Assert.IsFalse(actual.IsAscii);
            //// Assert.AreEqual(columndef.Columnid, actual.Columnid);
            Assert.AreEqual(this.testColumnid.Type, actual.Type);

            Assert.AreEqual(actual, columnCollection[TestColumnName.ToLower()]);
        }

        /// <summary>
        /// Create an index.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Create an index")]
        public void IsamCreateIndex()
        {
            const string IndexName = "new_index";

            using (IsamTransaction transaction = new IsamTransaction(this.sesid))
            {
                // Oddity: Why dcan't it be specified in the constructor?
                // KeyColumns = indexKeys,
                IndexDefinition newIndex = new IndexDefinition(IndexName) { Flags = IndexFlags.None, Density = 100, };

                var indexKey = new KeyColumn(this.testColumnid.Name, true);
                newIndex.KeyColumns.Add(indexKey);

                this.tableid.TableDefinition.CreateIndex(newIndex);

                transaction.Commit(false);

                this.tableid.SetCurrentIndex(IndexName);
            }
        }

        /// <summary>
        /// Create an index with an invalid name.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Create an index")]
        public void IsamCreateIndexInvalidName()
        {
            const string IndexName = "[BAD!NAME]";

            using (IsamTransaction transaction = new IsamTransaction(this.sesid))
            {
                // Oddity: Why dcan't it be specified in the constructor?
                // KeyColumns = indexKeys,
                IndexDefinition newIndex = new IndexDefinition(IndexName) { Flags = IndexFlags.None, Density = 100, };

                var indexKey = new KeyColumn(this.testColumnid.Name, true);
                newIndex.KeyColumns.Add(indexKey);

                try
                {
                    this.tableid.TableDefinition.CreateIndex(newIndex);
                    Assert.Fail("Expected exception not thrown.");
                }
                catch (Miei.EsentInvalidNameException)
                {
                }

                transaction.Commit(false);
            }
        }

        /// <summary>
        /// Verify that setting an index returns the name of the index.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify that setting an index returns the name of the index")]
        public void VerifyGetCurrentIndexReturnsIndexName()
        {
            const string IndexName = "NewIndexName";

            using (IsamTransaction transaction = new IsamTransaction(this.sesid))
            {
                // Oddity: Why dcan't it be specified in the constructor?
                // KeyColumns = indexKeys,
                IndexDefinition newIndex = new IndexDefinition(IndexName) { Flags = IndexFlags.None, Density = 100, };

                var indexKey = new KeyColumn(this.testColumnid.Name, true);
                newIndex.KeyColumns.Add(indexKey);

                this.tableid.TableDefinition.CreateIndex(newIndex);

                transaction.Commit(false);

                this.tableid.SetCurrentIndex(IndexName);

                string actualIndexName;
                actualIndexName = this.tableid.CurrentIndex;

                Assert.AreEqual(IndexName, actualIndexName);

                // Try to set it to null (the primary index)
                this.tableid.CurrentIndex = null;
                actualIndexName = this.tableid.CurrentIndex;
                Assert.AreEqual("PrimaryIndex", actualIndexName);
            }
        }

        /// <summary>
        /// Delete an index and make sure we can't use it afterwards.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Delete an index and make sure we can't use it afterwards")]
        public void IsamDropIndex()
        {
            const string IndexName = "index_to_delete";

            using (IsamTransaction transaction = new IsamTransaction(this.sesid))
            {
                // Oddity: Why dcan't it be specified in the constructor?
                // KeyColumns = indexKeys,
                IndexDefinition newIndex = new IndexDefinition(IndexName) { Flags = IndexFlags.None, Density = 100, };

                var indexKey = new KeyColumn(this.testColumnid.Name, true);
                newIndex.KeyColumns.Add(indexKey);

                this.tableid.TableDefinition.CreateIndex(newIndex);

                transaction.Commit(false);

                this.tableid.SetCurrentIndex(IndexName);
            }

            try
            {
                // Try deleting it, even though it is in use. Should fail.
                this.tableid.TableDefinition.DropIndex(IndexName);
                Assert.Fail("Index was deleted, even though it is in use!");
            }
            catch (Miei.EsentIndexInUseException)
            {
                // Expected exception thrown
            }

            // Set to the primary index.
            this.tableid.SetCurrentIndex(null);

            using (IsamTransaction transaction = new IsamTransaction(this.sesid))
            {
                this.tableid.TableDefinition.DropIndex(IndexName);
                transaction.Commit(false);
            }

            try
            {
                this.tableid.SetCurrentIndex(IndexName);
                Assert.Fail("Index is still visible");
            }
            catch (Miei.EsentErrorException)
            {
                // Expected exception thrown
            }
        }

        /// <summary>
        /// Delete a column and make sure we can't see it afterwards.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Delete a column and make sure we can't see it afterwards")]
        public void TestDropColumn()
        {
            const string ColumnName = "column_to_delete";
            int defaultValue = Any.Int32;

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                var columndef = new ColumnDefinition(ColumnName) { Type = typeof(int), DefaultValue = defaultValue, };
                this.tableid.TableDefinition.AddColumn(columndef);

                trx.Commit(false);
            }

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                this.tableid.TableDefinition.DropColumn(ColumnName);
                trx.Commit(false);

                try
                {
                    var columndef = this.tableid.TableDefinition.Columns[ColumnName];
                    Assert.Fail("Column is still visible");
                }
                catch (Miei.EsentColumnNotFoundException)
                {
                }
            }
        }

        /// <summary>
        /// Delete a table and make sure we can't see it afterwards.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Delete a table and make sure we can't see it afterwards")]
        public void IsamDeleteTable()
        {
            const string TableName = "table_to_delete";
            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                var tabledef = new TableDefinition(TableName);
                this.dbid.CreateTable(tabledef);
                trx.Commit(false);
            }

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                this.dbid.DropTable(TableName);
                trx.Commit(false);
            }

            try
            {
                Cursor cursor = this.dbid.OpenCursor(TableName, false);
                Assert.Fail("Column is still visible");
            }
            catch (Miei.EsentObjectNotFoundException)
            {
            }
        }

        /// <summary>
        /// Verify an error is thrown when key is truncated and truncation is disallowed.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify an error is thrown when key is truncated and truncation is disallowed")]
        public void TestDisallowTruncation()
        {
            const string IndexName = "no_trunacation_index";

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                IndexDefinition indexdef = new IndexDefinition(IndexName);
                indexdef.KeyColumns.Add(new KeyColumn(IsamDdlTests.TestColumnName, true));
                indexdef.Flags = IndexFlags.DisallowTruncation;

                this.tableid.TableDefinition.CreateIndex(indexdef);
                trx.Commit(false);
            }

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                this.tableid.BeginEditForInsert();
                this.tableid.EditRecord[this.testColumnid.Name] = new string('X', 4096);
                try
                {
                    this.tableid.AcceptChanges();
                    Assert.Fail("Expected a truncation error");
                }
                catch (Miei.EsentKeyTruncatedException)
                {
                    // Expected
                }

                trx.Rollback();
            }

            this.tableid.TableDefinition.DropIndex(IndexName);
        }

        /// <summary>
        /// Verify no error is thrown when key is truncated and truncation is allowed.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify no error is thrown when key is truncated and truncation is allowed.")]
        public void TestAllowTruncation()
        {
            const string TableName = "truncationtable";
            const string IntColumnOne = "TestColumn1";
            const string IntColumnTwo = "TestColumn2";

            var tabledef = new TableDefinition(TableName);
            var columndef = new ColumnDefinition(TestColumnName) { Type = typeof(string), };

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                tabledef.Columns.Add(columndef);
                tabledef.Columns.Add(new ColumnDefinition(IntColumnOne) { Type = typeof(int), });
                tabledef.Columns.Add(new ColumnDefinition(IntColumnTwo) { Type = typeof(int), });

                var primaryIndex = new IndexDefinition("PrimaryIndex")
                                       {
                                           KeyColumns =
                                               {
                                                   new KeyColumn(
                                                       IntColumnOne,
                                                       true),
                                               },
                                       };
                primaryIndex.Flags = IndexFlags.AllowTruncation | IndexFlags.Primary;

                // You can't call CreateIndex on the local tabledef. You must
                // tabledef.CreateIndex(primaryIndex);
//                this.dbid.CreateTable(tabledef);
                tabledef.Indices.Add(primaryIndex);
                this.dbid.CreateTable(tabledef);
                //                using (var cursor = this.dbid.OpenCursor(TableName))
//                {
//                    cursor.TableDefinition.CreateIndex(primaryIndex);
//                }

                trx.Commit(false);
            }

            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                using (var tableid = this.dbid.OpenCursor(TableName))
                {
                    tableid.BeginEditForInsert();
                    tableid.EditRecord[columndef.Name] = new string('X', 4096);

                    tableid.AcceptChanges();

                    trx.Commit(false);
                }
            }

            this.dbid.DropTable(TableName);
        }

        /// <summary>
        /// Verify Cursor.Dispose is resilient to rollbacks.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify Cursor.Dispose is resilient to rollbacks.")]
        public void TestCursorDisposeAfterRollback()
        {
            using (IsamTransaction trx = new IsamTransaction(this.sesid))
            {
                using (var tableid = this.dbid.OpenCursor(this.tableName))
                {
                    // Issue: This is not safe. It actually crashes, because
                    // rolling back a transaction will close the underlying table.
                    // trx.Rollback();
                }
            }
        }

        /// <summary>
        /// Verify the fields returned are correct.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify the fields returned are correct.")]
        public void TestCursorDotFields()
        {
            const string TestTableName = "testenumtable";
            const string IntColumnOne = "column1";
            const string IntColumnTwo = "columnTwo";

            var tableDefinition = new TableDefinition(TestTableName);
            tableDefinition.Columns.Add(new ColumnDefinition(IntColumnOne)
            {
                Type = typeof(int),
            });
            tableDefinition.Columns.Add(new ColumnDefinition(IntColumnTwo)
            {
                Type = typeof(int)
            });

            var idx = new IndexDefinition("idx1");
            idx.KeyColumns.Add(new KeyColumn("column1", true));
            tableDefinition.Indices.Add(idx);
            this.dbid.CreateTable(tableDefinition);

            using (var cursor = this.dbid.OpenCursor(TestTableName))
            {
                cursor.BeginEditForInsert();
                cursor.EditRecord[IntColumnOne] = 1;

                // here it goes key not found when it tries to retrieve columndefinition from the db
                cursor.EditRecord[IntColumnTwo] = 2;
                cursor.AcceptChanges();

                cursor.MoveBeforeFirst();
                cursor.MoveNext();
                FieldCollection fields = cursor.Fields;
                Assert.IsTrue(fields.Contains(IntColumnOne));
                Assert.IsTrue(fields.Contains(IntColumnTwo));
                Assert.AreEqual(2, fields.Count);
            }
            /*
                        this.tableid.BeginEditForInsert();
                            this.tableid.EditRecord[IntColumnOne] = 1;

                            // here it goes key not found when it tries to retrieve columndefinition from the db
                            this.tableid.EditRecord[IntColumnTwo] = 2;
                            this.tableid.AcceptChanges();
                        this.tableid.MoveBeforeFirst();
                        this.tableid.MoveNext();
                        FieldCollection fields = this.tableid.Fields;
                        Assert.IsTrue(fields.Contains(IntColumnOne));
                        Assert.IsTrue(fields.Contains(IntColumnTwo));
                        Assert.AreEqual(2, fields.Count);
                         * */


            this.dbid.DropTable(TestTableName);
        }

        #endregion DDL Tests
    }
}
