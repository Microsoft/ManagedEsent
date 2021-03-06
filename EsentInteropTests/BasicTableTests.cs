//-----------------------------------------------------------------------
// <copyright file="BasicTableTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.
// </copyright>
//-----------------------------------------------------------------------

namespace InteropApiTests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Microsoft.Isam.Esent.Interop;
    using Microsoft.Isam.Esent.Interop.Vista;
    using Microsoft.Isam.Esent.Interop.Windows7;
    using Microsoft.Isam.Esent.Interop.Windows8;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Basic Api tests
    /// </summary>
    [TestClass]
    public partial class BasicTableTests
    {
        /// <summary>
        /// The directory being used for the database and its files.
        /// </summary>
        private string directory;

        /// <summary>
        /// The path to the database being used by the test.
        /// </summary>
        private string database;

        /// <summary>
        /// The name of the table.
        /// </summary>
        private string table;

        /// <summary>
        /// The instance used by the test.
        /// </summary>
        private JET_INSTANCE instance;

        /// <summary>
        /// The session used by the test.
        /// </summary>
        private JET_SESID sesid;

        /// <summary>
        /// Identifies the database used by the test.
        /// </summary>
        private JET_DBID dbid;

        /// <summary>
        /// The tableid being used by the test.
        /// </summary>
        private JET_TABLEID tableid;

        /// <summary>
        /// Columnid of the LongText column in the table.
        /// </summary>
        private JET_COLUMNID columnidLongText;

        #region Setup/Teardown

        /// <summary>
        /// Initialization method. Called once when the tests are started.
        /// All DDL should be done in this method.
        /// </summary>
        [TestInitialize]
        [Description("Setup for BasicTableTests")]
        public void Setup()
        {
            this.directory = SetupHelper.CreateRandomDirectory();
            this.database = Path.Combine(this.directory, "database.edb");
            this.table = "table";
            this.instance = SetupHelper.CreateNewInstance(this.directory);

            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.Recovery, 0, "on");
            Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, JET_param.MaxTemporaryTables, 0, null);
            Api.JetInit(ref this.instance);
            Api.JetBeginSession(this.instance, out this.sesid, string.Empty, string.Empty);
            Api.JetCreateDatabase(this.sesid, this.database, string.Empty, out this.dbid, CreateDatabaseGrbit.None);
            Api.JetBeginTransaction(this.sesid);
            Api.JetCreateTable(this.sesid, this.dbid, this.table, 0, 100, out this.tableid);

            var columndef = new JET_COLUMNDEF()
            {
                cp = JET_CP.Unicode,
                coltyp = JET_coltyp.LongText,
            };
            Api.JetAddColumn(this.sesid, this.tableid, "TestColumn", columndef, null, 0, out this.columnidLongText);

            Api.JetCloseTable(this.sesid, this.tableid);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);
            Api.JetOpenTable(this.sesid, this.dbid, this.table, null, 0, OpenTableGrbit.None, out this.tableid);
        }

        /// <summary>
        /// Cleanup after all tests have run.
        /// </summary>
        [TestCleanup]
        [Description("Cleanup for BasicTableTests")]
        public void Teardown()
        {
            Api.JetCloseTable(this.sesid, this.tableid);
            Api.JetEndSession(this.sesid, EndSessionGrbit.None);
            Api.JetTerm(this.instance);
            Cleanup.DeleteDirectoryWithRetry(this.directory);
        }

        /// <summary>
        /// Verify that BasicTableTests has setup the test fixture properly.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify that BasicTableTests has setup the test fixture properly")]
        public void VerifyFixtureSetup()
        {
            Assert.IsNotNull(this.table);
            Assert.AreNotEqual(JET_INSTANCE.Nil, this.instance);
            Assert.AreNotEqual(JET_SESID.Nil, this.sesid);
            Assert.AreNotEqual(JET_DBID.Nil, this.dbid);
            Assert.AreNotEqual(JET_TABLEID.Nil, this.tableid);
            Assert.AreNotEqual(JET_COLUMNID.Nil, this.columnidLongText);

            JET_COLUMNDEF columndef;
            Api.JetGetTableColumnInfo(this.sesid, this.tableid, this.columnidLongText, out columndef);
            Assert.AreEqual(JET_coltyp.LongText, columndef.coltyp);
        }

        #endregion Setup/Teardown

        #region Session tests

#if !MANAGEDESENT_ON_WSA // The threading model in Windows Store Apps has changed.
        /// <summary>
        /// Test moving a transaction between threads.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test moving a transaction between threads")]
        public void VerifyJetSetSessionContextAllowsThreadMigration()
        {
            // Without the calls to JetSetSessionContext/JetResetSessionContext
            // this will generate a session sharing violation.
            var context = new IntPtr(Any.Int32);

            var thread = new Thread(() =>
                {
                    EseInteropTestHelper.ThreadBeginThreadAffinity();
                    Api.JetSetSessionContext(this.sesid, context);
                    Api.JetBeginTransaction(this.sesid);
                    Api.JetResetSessionContext(this.sesid);
                    EseInteropTestHelper.ThreadEndThreadAffinity();
                });
            thread.Start();
            thread.Join();

            Api.JetSetSessionContext(this.sesid, context);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.None);
            Api.JetResetSessionContext(this.sesid);
        }
#endif // !MANAGEDESENT_ON_WSA

        #endregion

        #region JetDupCursor

#if !MANAGEDESENT_ON_WSA // Not exposed in MSDK
        /// <summary>
        /// Verify JetDupCursor returns a different tableid.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify JetDupCursor returns a different tableid")]
        public void VerifyJetDupCursorReturnsDifferentTableid()
        {
            JET_TABLEID newTableid;
            Api.JetDupCursor(this.sesid, this.tableid, out newTableid, DupCursorGrbit.None);
            Assert.AreNotEqual(newTableid, this.tableid);
            Api.JetCloseTable(this.sesid, newTableid);
        }

        /// <summary>
        /// Verify JetDupCursor should returns a tableid on the same table.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify JetDupCursor should returns a tableid on the same table")]
        public void VerifyJetDupCursorReturnsCursorOnSameTable()
        {
            string expected = Any.String;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(expected);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            JET_TABLEID newTableid;
            Api.JetDupCursor(this.sesid, this.tableid, out newTableid, DupCursorGrbit.None);
            Api.JetMove(this.sesid, newTableid, JET_Move.First, MoveGrbit.None);
            string actual = Api.RetrieveColumnAsString(this.sesid, newTableid, this.columnidLongText, Encoding.Unicode);
            Assert.AreEqual(expected, actual);
            Api.JetCloseTable(this.sesid, newTableid);
        }
#endif // !MANAGEDESENT_ON_WSA

        #endregion JetDupCursor

        #region DML Tests

        /// <summary>
        /// Insert a record and retrieve it.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and retrieve it")]
        public void InsertRecord()
        {
            string s = Any.String;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(s);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);
            Assert.AreEqual(s, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a compressed record and retrieve it.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a compressed record and retrieve it")]
        public void InsertCompressedRecord()
        {
            string s = Any.String;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetCompressedColumnFromString(s);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);
            Assert.AreEqual(s, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a record and update it.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and update it")]
        public void ReplaceRecord()
        {
            string before = Any.String;
            string after = Any.String;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(before);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Replace);
            this.SetColumnFromString(after);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Assert.AreEqual(after, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a record and update it. This uses the Transaction class.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Inserts a record and update it. This uses the Transaction class")]
        public void ReplaceRecordWithTransactionClass()
        {
            string before = Any.String;
            string after = Any.String;

            using (var transaction = new Transaction(this.sesid))
            {
                Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
                this.SetColumnFromString(before);
                this.UpdateAndGotoBookmark();
                transaction.Commit(CommitTransactionGrbit.LazyFlush);
            }

            using (var transaction = new Transaction(this.sesid))
            {
                Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Replace);
                this.SetColumnFromString(after);
                this.UpdateAndGotoBookmark();
                transaction.Commit(CommitTransactionGrbit.LazyFlush);
            }

            Assert.AreEqual(after, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a record, update it and rollback the update.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Inserts a record, update it and rollback the update")]
        public void ReplaceAndRollback()
        {
            string before = Any.String;
            string after = Any.String;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(before);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Replace);
            this.SetColumnFromString(after);
            this.UpdateAndGotoBookmark();
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);

            Assert.AreEqual(before, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Using JetBeginTransaction2, insert a record, update it and rollback the update.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Using JetBeginTransaction2, insert a record, update it and rollback the update")]
        public void TestJetBeginTransaction2()
        {
            string before = Any.String;
            string after = Any.String;

            Api.JetBeginTransaction2(this.sesid, BeginTransactionGrbit.None);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(before);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Replace);
            this.SetColumnFromString(after);
            this.UpdateAndGotoBookmark();
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);

            Assert.AreEqual(before, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Using JetBeginTransaction3, insert a record, update it and rollback the update.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Using JetBeginTransaction3, insert a record, update it and rollback the update")]
        public void TestJetBeginTransaction3()
        {
            if (!EsentVersion.SupportsWindows8Features)
            {
                return;
            }

            string before = Any.String;
            string after = Any.String;

            Windows8Api.JetBeginTransaction3(this.sesid, 20120131, BeginTransactionGrbit.None);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(before);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Replace);
            this.SetColumnFromString(after);
            this.UpdateAndGotoBookmark();
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);

            Assert.AreEqual(before, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a record, update it and rollback the transaction.
        /// This uses the Transaction class.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record, update it and rollback the transaction. This uses the Transaction class")]
        public void ReplaceAndRollbackWithTransactionClass()
        {
            string before = Any.String;
            string after = Any.String;

            using (var transaction = new Transaction(this.sesid))
            {
                Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
                this.SetColumnFromString(before);
                this.UpdateAndGotoBookmark();
                transaction.Commit(CommitTransactionGrbit.LazyFlush);
            }

            using (var transaction = new Transaction(this.sesid))
            {
                Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Replace);
                this.SetColumnFromString(after);
                this.UpdateAndGotoBookmark();

                // the transaction isn't committed
            }

            Assert.AreEqual(before, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a record and delete it.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and delete it")]
        public void InsertRecordAndDelete()
        {
            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetDelete(this.sesid, this.tableid);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            try
            {
                string x = this.RetrieveColumnAsString();
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.RecordDeleted, ex.Error);
            }
        }

#if !MANAGEDESENT_ON_WSA // Not exposed in MSDK
        /// <summary>
        /// Test JetGetLock.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test JetGetLock")]
        public void JetGetLock()
        {
            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetGetLock(this.sesid, this.tableid, GetLockGrbit.Read);
            Api.JetGetLock(this.sesid, this.tableid, GetLockGrbit.Write);
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);
        }

        /// <summary>
        /// Verify that JetGetLock throws an exception when incompatible
        /// locks are requested.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify that JetGetLock throws an exception when incompatible locks are requested")]
        public void VerifyJetGetLockThrowsExceptionOnWriteConflict()
        {
            var bookmark = new byte[SystemParameters.BookmarkMost];
            int bookmarkSize;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetUpdate(this.sesid, this.tableid, bookmark, bookmark.Length, out bookmarkSize);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            JET_SESID otherSesid;
            JET_DBID otherDbid;
            JET_TABLEID otherTableid;
            Api.JetDupSession(this.sesid, out otherSesid);
            Api.JetOpenDatabase(otherSesid, this.database, null, out otherDbid, OpenDatabaseGrbit.None);
            Api.JetOpenTable(otherSesid, otherDbid, this.table, null, 0, OpenTableGrbit.None, out otherTableid);

            Api.JetGotoBookmark(this.sesid, this.tableid, bookmark, bookmarkSize);
            Api.JetGotoBookmark(otherSesid, otherTableid, bookmark, bookmarkSize);

            Api.JetBeginTransaction(this.sesid);
            Api.JetBeginTransaction(otherSesid);

            Api.JetGetLock(this.sesid, this.tableid, GetLockGrbit.Read);
            try
            {
                Api.JetGetLock(otherSesid, otherTableid, GetLockGrbit.Write);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException ex)
            {
                Assert.AreEqual(JET_err.WriteConflict, ex.Error);
            }

            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);
            Api.JetRollback(otherSesid, RollbackTransactionGrbit.None);

            Api.JetCloseTable(otherSesid, otherTableid);
            Api.JetCloseDatabase(otherSesid, otherDbid, CloseDatabaseGrbit.None);
            Api.JetEndSession(otherSesid, EndSessionGrbit.None);
        }

        /// <summary>
        /// Test JetGetLock.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test TryGetLock")]
        public void TestTryGetLock()
        {
            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Assert.IsTrue(Api.TryGetLock(this.sesid, this.tableid, GetLockGrbit.Read));
            Assert.IsTrue(Api.TryGetLock(this.sesid, this.tableid, GetLockGrbit.Write));
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);
        }

        /// <summary>
        /// Verify that TryGetLock returns false when incompatible locks are requested.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify that TryGetLock returns false when incompatible locks are requested")]
        public void VerifyTryGetLockReturnsFalseOnWriteConflict()
        {
            var bookmark = new byte[SystemParameters.BookmarkMost];
            int bookmarkSize;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetUpdate(this.sesid, this.tableid, bookmark, bookmark.Length, out bookmarkSize);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            JET_SESID otherSesid;
            JET_DBID otherDbid;
            JET_TABLEID otherTableid;
            Api.JetDupSession(this.sesid, out otherSesid);
            Api.JetOpenDatabase(otherSesid, this.database, null, out otherDbid, OpenDatabaseGrbit.None);
            Api.JetOpenTable(otherSesid, otherDbid, this.table, null, 0, OpenTableGrbit.None, out otherTableid);

            Api.JetGotoBookmark(this.sesid, this.tableid, bookmark, bookmarkSize);
            Api.JetGotoBookmark(otherSesid, otherTableid, bookmark, bookmarkSize);

            Api.JetBeginTransaction(this.sesid);
            Api.JetBeginTransaction(otherSesid);

            Assert.IsTrue(Api.TryGetLock(this.sesid, this.tableid, GetLockGrbit.Read));
            Assert.IsFalse(Api.TryGetLock(otherSesid, otherTableid, GetLockGrbit.Write));

            // Rollback the the first transaction which will let the second transaction
            // lock the record.
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);

            Assert.IsTrue(Api.TryGetLock(otherSesid, otherTableid, GetLockGrbit.Write));

            Api.JetRollback(otherSesid, RollbackTransactionGrbit.None);

            Api.JetCloseTable(otherSesid, otherTableid);
            Api.JetCloseDatabase(otherSesid, otherDbid, CloseDatabaseGrbit.None);
            Api.JetEndSession(otherSesid, EndSessionGrbit.None);
        }

        /// <summary>
        /// Call JetComputeStats.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Call JetComputeStats")]
        public void JetComputeStats()
        {
            Api.JetBeginTransaction(this.sesid);
            for (int i = 0; i < 10; ++i)
            {
                Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
                this.SetColumnFromString(Any.String);
                this.UpdateAndGotoBookmark();
            }

            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);
            Api.JetComputeStats(this.sesid, this.tableid);
        }

        /// <summary>
        /// Call JetGetRecordSize with intrinsic data.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Call JetGetRecordSize with intrinsic data")]
        public void JetGetRecordSizeIntrinsic()
        {
            if (!EsentVersion.SupportsVistaFeatures)
            {
                return;
            }

            var size = new JET_RECSIZE();

            byte[] data = Any.Bytes;
            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetSetColumn(this.sesid, this.tableid, this.columnidLongText, data, data.Length, SetColumnGrbit.None, null);
            this.UpdateAndGotoBookmark();
            VistaApi.JetGetRecordSize(this.sesid, this.tableid, ref size, GetRecordSizeGrbit.None);

            Assert.AreEqual(data.Length, size.cbData, "cbData");
            Assert.AreEqual(data.Length, size.cbDataCompressed, "cbDataCompressed");
            Assert.AreEqual(0, size.cbLongValueData, "cbLongValueData");
            Assert.AreEqual(0, size.cbLongValueDataCompressed, "cbLongValueDataCompressed");
            Assert.AreEqual(0, size.cbLongValueOverhead, "cbLongValueOverhead");
            Assert.AreNotEqual(0, size.cbOverhead, "cbOverhead");
            Assert.AreEqual(0, size.cCompressedColumns, "cCompressedColumns");
            Assert.AreEqual(0, size.cLongValues, "cLongValues");
            Assert.AreEqual(0, size.cMultiValues, "cMultiValues");
            Assert.AreEqual(0, size.cNonTaggedColumns, "cTaggedColumns");
            Assert.AreEqual(1, size.cTaggedColumns, "cTaggedColumns");
        }

        /// <summary>
        /// Call JetGetRecordSize with separated data. This also tests the RunningTotal option.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Call JetGetRecordSize with separated data")]
        public void JetGetRecordSizeSeparated()
        {
            if (!EsentVersion.SupportsVistaFeatures)
            {
                return;
            }

            var size = new JET_RECSIZE();

            byte[] data = Any.BytesOfLength(64);
            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetSetColumn(this.sesid, this.tableid, this.columnidLongText, data, data.Length, SetColumnGrbit.SeparateLV, null);
            VistaApi.JetGetRecordSize(this.sesid, this.tableid, ref size, GetRecordSizeGrbit.InCopyBuffer);
            this.UpdateAndGotoBookmark();
            VistaApi.JetGetRecordSize(this.sesid, this.tableid, ref size, GetRecordSizeGrbit.RunningTotal);

            Assert.AreEqual(0, size.cbData, "cbData");
            Assert.AreEqual(0, size.cbDataCompressed, "cbDataCompressed");
            Assert.AreEqual(data.Length * 2, size.cbLongValueData, "cbLongValueData");
            Assert.AreEqual(data.Length * 2, size.cbLongValueDataCompressed, "cbLongValueDataCompressed");
            Assert.AreNotEqual(0, size.cbLongValueOverhead, "cbLongValueOverhead");
            Assert.AreNotEqual(0, size.cbOverhead, "cbOverhead");
            Assert.AreEqual(0, size.cCompressedColumns, "cCompressedColumns");
            Assert.AreEqual(2, size.cLongValues, "cLongValues");
            Assert.AreEqual(0, size.cMultiValues, "cMultiValues");
            Assert.AreEqual(0, size.cNonTaggedColumns, "cTaggedColumns");
            Assert.AreEqual(2, size.cTaggedColumns, "cTaggedColumns");
        }

        /// <summary>
        /// Test JetGetCursorInfo.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test JetGetCursorInfo")]
        public void TestJetGetCursorInfo()
        {
            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.UpdateAndGotoBookmark();
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(this.sesid);
            Api.JetGetCursorInfo(this.sesid, this.tableid);
            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);
        }

        /// <summary>
        /// Test Api.OpenTable overload.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test Api.OpenTable.")]
        public void TestApiOpenTable()
        {
            JET_SESID sesid2;
            JET_DBID dbid2;
            JET_TABLEID tableid2;
            Api.BeginSession(this.instance, out sesid2);
            Api.OpenDatabase(sesid2, this.database, out dbid2, OpenDatabaseGrbit.None);
            Api.OpenTable(sesid2, dbid2, this.table, OpenTableGrbit.None, out tableid2);

            Api.JetEndSession(sesid2, EndSessionGrbit.None);
        }

        /// <summary>
        /// Test Api.TryOpenTable overload.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test Api.TryOpenTable.")]
        public void TestApiTryOpenTable()
        {
            JET_SESID sesid2;
            JET_DBID dbid2;
            JET_TABLEID tableid2;
            Api.BeginSession(this.instance, out sesid2);
            Api.OpenDatabase(sesid2, this.database, out dbid2, OpenDatabaseGrbit.None);
            Assert.IsTrue(Api.TryOpenTable(sesid2, dbid2, this.table, OpenTableGrbit.None, out tableid2));

            Api.JetEndSession(sesid2, EndSessionGrbit.None);
        }

        /// <summary>
        /// Test Api.TryOpenTable overload that fails to open a table.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test Api.TryOpenTable overload that fails to open a table.")]
        public void TestApiTryOpenTableFail()
        {
            JET_SESID sesid2;
            JET_DBID dbid2;
            JET_TABLEID tableid2;
            Api.BeginSession(this.instance, out sesid2);
            Api.OpenDatabase(sesid2, this.database, out dbid2, OpenDatabaseGrbit.None);
            Assert.IsFalse(Api.TryOpenTable(sesid2, dbid2, "does_not_exist", OpenTableGrbit.None, out tableid2));

            Api.JetEndSession(sesid2, EndSessionGrbit.None);
        }

        /// <summary>
        /// Test JetGetCursorInfo when there is a write conflict.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test JetGetCursorInfo when there is a write conflict")]
        public void TestJetGetCursorInfoWithWriteConflict()
        {
            byte[] bookmark = new byte[SystemParameters.BookmarkMost];
            int bookmarkSize;

            JET_SESID sesid2;
            JET_DBID dbid2;
            JET_TABLEID tableid2;
            Api.JetBeginSession(this.instance, out sesid2, string.Empty, string.Empty);
            Api.JetOpenDatabase(sesid2, this.database, string.Empty, out dbid2, OpenDatabaseGrbit.None);
            Api.JetOpenTable(sesid2, dbid2, this.table, null, 0, OpenTableGrbit.None, out tableid2);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetUpdate(this.sesid, this.tableid, bookmark, bookmark.Length, out bookmarkSize);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            Api.JetBeginTransaction(sesid2);
            Api.JetGotoBookmark(sesid2, tableid2, bookmark, bookmarkSize);
            Api.JetPrepareUpdate(sesid2, tableid2, JET_prep.Replace);
            Api.SetColumn(sesid2, tableid2, this.columnidLongText, "foo", Encoding.Unicode);
            Api.JetUpdate(sesid2, tableid2);

            Api.JetBeginTransaction(this.sesid);
            Api.JetGotoBookmark(this.sesid, this.tableid, bookmark, bookmarkSize);
            try
            {
                Api.JetGetCursorInfo(this.sesid, this.tableid);
                Assert.Fail("Expected an EsentErrorException");
            }
            catch (EsentErrorException)
            {
                // Expected
            }

            Api.JetRollback(this.sesid, RollbackTransactionGrbit.None);
            Api.JetCommitTransaction(sesid2, CommitTransactionGrbit.None);
            Api.JetEndSession(sesid2, EndSessionGrbit.None);
        }
#endif // !MANAGEDESENT_ON_WSA

        #endregion DML Tests

        #region Navigation Tests

        /// <summary>
        /// Insert a record and retrieve its bookmark.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and retrieve its bookmark")]
        public void JetGetBookmark()
        {
            var expectedBookmark = new byte[256];
            int expectedBookmarkSize;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetUpdate(this.sesid, this.tableid, expectedBookmark, expectedBookmark.Length, out expectedBookmarkSize);
            Api.JetGotoBookmark(this.sesid, this.tableid, expectedBookmark, expectedBookmarkSize);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            var actualBookmark = new byte[256];
            int actualBookmarkSize;
            Api.JetGetBookmark(this.sesid, this.tableid, actualBookmark, actualBookmark.Length, out actualBookmarkSize);

            Assert.AreEqual(expectedBookmarkSize, actualBookmarkSize);
            for (int i = 0; i < expectedBookmarkSize; ++i)
            {
                Assert.AreEqual(expectedBookmark[i], actualBookmark[i]);
            }
        }

        /// <summary>
        /// Insert a record and retrieve its bookmark.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and retrieve its bookmark")]
        public void GetBookmark()
        {
            var expectedBookmark = new byte[256];
            int expectedBookmarkSize;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetUpdate(this.sesid, this.tableid, expectedBookmark, expectedBookmark.Length, out expectedBookmarkSize);
            Api.JetGotoBookmark(this.sesid, this.tableid, expectedBookmark, expectedBookmarkSize);
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);

            byte[] actualBookmark = Api.GetBookmark(this.sesid, this.tableid);

            Assert.AreEqual(expectedBookmarkSize, actualBookmark.Length);
            for (int i = 0; i < expectedBookmarkSize; ++i)
            {
                Assert.AreEqual(expectedBookmark[i], actualBookmark[i]);
            }
        }

        /// <summary>
        /// TryGotoBookmark for both positive and negative cases.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("TryGotoBookmark for both positive and negative cases.")]
        public void TryGotoBookmark()
        {
            var bookmark = new byte[256];
            int bookmarkSize;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            Api.JetUpdate(this.sesid, this.tableid, bookmark, bookmark.Length, out bookmarkSize);
            Assert.AreEqual(true, Api.TryGotoBookmark(this.sesid, this.tableid, bookmark, bookmarkSize));
            Api.JetDelete(this.sesid, this.tableid);
            Assert.AreEqual(false, Api.TryGotoBookmark(this.sesid, this.tableid, bookmark, bookmarkSize));
            Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.LazyFlush);
        }

        /// <summary>
        /// Insert a record and retrieve its key.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and retrieve its key")]
        public void JetRetrieveKey()
        {
            string expected = Any.String;
            var key = new byte[8192];

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(expected);
            this.UpdateAndGotoBookmark();

            int keyLength;
            Api.JetRetrieveKey(this.sesid, this.tableid, key, key.Length, out keyLength, RetrieveKeyGrbit.None);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.UpdateAndGotoBookmark();

            Api.JetMakeKey(this.sesid, this.tableid, key, keyLength, MakeKeyGrbit.NormalizedKey);
            Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekEQ);
            Assert.AreEqual(expected, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Insert a record and retrieve its key.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Insert a record and retrieve its key")]
        public void RetrieveKey()
        {
            string expected = Any.String;

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.SetColumnFromString(expected);
            this.UpdateAndGotoBookmark();

            byte[] key = Api.RetrieveKey(this.sesid, this.tableid, RetrieveKeyGrbit.None);

            Api.JetBeginTransaction(this.sesid);
            Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
            this.UpdateAndGotoBookmark();

            Api.JetMakeKey(this.sesid, this.tableid, key, key.Length, MakeKeyGrbit.NormalizedKey);
            Api.JetSeek(this.sesid, this.tableid, SeekGrbit.SeekEQ);
            Assert.AreEqual(expected, this.RetrieveColumnAsString());
        }

        /// <summary>
        /// Preread keys.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Test JetPrereadKeys")]
        public void JetPrereadKeys()
        {
            if (!EsentVersion.SupportsWindows7Features)
            {
                return;
            }

            // We need enough records to force a vertical split. ESENT returns
            // 0 for keysPreread when we have a single-level tree.
            const int NumRecords = 1024;
            byte[][] keys = new byte[NumRecords][];
            int[] keyLengths = new int[NumRecords];

            // Setting a fixed cache size improves reliability of this test case because,
            // due to optimization reasons, we may drop pre-reads with a small cache size.
            // But first, we'll set the cache to a very small value and only grow it later
            // so that we know for sure we'll have available buffers.
            int cacheSizeMinOriginal = SystemParameters.CacheSizeMin;
            int cacheSizeMaxOriginal = SystemParameters.CacheSizeMax;
            SystemParameters.CacheSizeMin = 128;
            SystemParameters.CacheSizeMax = 128;

            try
            {
                Api.JetBeginTransaction(this.sesid);

                // This table uses a sequential index so the records will
                // be in key order.
                for (int i = 0; i < NumRecords; ++i)
                {
                    Api.JetPrepareUpdate(this.sesid, this.tableid, JET_prep.Insert);
                    this.SetColumnFromString(Any.StringOfLength(1024));
                    this.UpdateAndGotoBookmark();

                    keys[i] = Api.RetrieveKey(this.sesid, this.tableid, RetrieveKeyGrbit.None);
                    keyLengths[i] = keys[i].Length;
                }

                Api.JetCommitTransaction(this.sesid, CommitTransactionGrbit.None);

                SystemParameters.CacheSizeMin = 1024;
                SystemParameters.CacheSizeMax = 1024;

                int keysPreread;
                Windows7Api.JetPrereadKeys(this.sesid, this.tableid, keys, keyLengths, NumRecords, out keysPreread, PrereadKeysGrbit.Forward);
                Assert.AreNotEqual(0, keysPreread, "No keys were preread?!");
            }
            finally
            {
                SystemParameters.CacheSizeMin = cacheSizeMinOriginal;
                SystemParameters.CacheSizeMax = cacheSizeMaxOriginal;
            }
        }

        /// <summary>
        ///  JetGetBookmark throws exception when no currency is set.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("JetGetBookmark throws exception when no currency is set")]
        [ExpectedException(typeof(EsentNoCurrentRecordException))]
        public void JetGetBookmarkThrowsExceptionWhenNoCurrencyIsSet()
        {
            var actualBookmark = new byte[1];
            int actualBookmarkSize;
            Api.JetGetBookmark(this.sesid, this.tableid, actualBookmark, actualBookmark.Length, out actualBookmarkSize);
        }

        /// <summary>
        ///  JetRetrieveKey throws exception when no currency is set.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("JetRetrieveKey throws exception when no currency is set")]
        [ExpectedException(typeof(EsentNoCurrentRecordException))]
        public void JetRetrieveKeyThrowsExceptionWhenNoCurrencyIsSet()
        {
            var key = new byte[1];

            int keyLength;
            Api.JetRetrieveKey(this.sesid, this.tableid, key, key.Length, out keyLength, RetrieveKeyGrbit.None);
        }

        /// <summary>
        /// JetRetrieveColumn throws exception when no currency is set.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("JetRetrieveColumn throws exception when no currency is set")]
        [ExpectedException(typeof(EsentNoCurrentRecordException))]
        public void JetRetrieveColumnThrowsExceptionWhenNoCurrencyIsSet()
        {
            int actualSize;
            var data = new byte[1];
            Api.JetRetrieveColumn(this.sesid, this.tableid, this.columnidLongText, data, 0, out actualSize, RetrieveColumnGrbit.None, null);
        }

        #endregion Navigation Tests

        #region JetDefragment Tests

        /// <summary>
        /// Test starting and stopping OLD with JetDefragment.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Start and stop online defragmentation with JetDefragment")]
        public void TestStartAndStopJetDefragment()
        {
            int passes = 1;
            int seconds = 1;
            Api.JetDefragment(this.sesid, this.dbid, null, ref passes, ref seconds, DefragGrbit.BatchStart);
            Api.JetDefragment(this.sesid, this.dbid, null, ref passes, ref seconds, DefragGrbit.BatchStop);
        }

        /// <summary>
        /// Test starting and stoppping OLD with <see cref="Api.Defragment"/>.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Start and stop online defragmentation with Api.Defragment")]
        public void TestStartAndStopApiDefragment()
        {
            Api.Defragment(this.sesid, this.dbid, null, DefragGrbit.BatchStart);
            Api.Defragment(this.sesid, this.dbid, null, DefragGrbit.BatchStop);
        }

        /// <summary>
        /// Test starting legacy OLD on read-only database.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Start legacy OLD on read-only database must fail")]
        public void TestStartLegacyOldReadOnlyDbMustFail()
        {
            this.ReattachDatabase(AttachDatabaseGrbit.ReadOnly);
            try
            {
                Api.Defragment(this.sesid, this.dbid, null, DefragGrbit.BatchStart);
                Assert.Fail("Starting legacy OLD should have failed with EsentDatabaseFileReadOnlyException, but succeeded.");
            }
            catch (EsentDatabaseFileReadOnlyException)
            {
                // Expected.
            }
            finally
            {
                this.ReattachDatabase(AttachDatabaseGrbit.None);
            }
        }

        /// <summary>
        /// Test starting OLD2 with DefragmentBTree on read-only database.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Start OLD2 with DefragmentBTree on read-only database must fail")]
        public void TestStartOld2WithDefragmentBTreeReadOnlyDbMustFail()
        {
            this.ReattachDatabase(AttachDatabaseGrbit.ReadOnly);
            try
            {
                DefragGrbit defragGrbit = Windows7Grbits.DefragmentBTree;
                Api.Defragment(this.sesid, this.dbid, this.table, defragGrbit);
                Assert.Fail("Starting OLD2 with {0} should have failed with EsentDatabaseFileReadOnlyException, but succeeded.", defragGrbit);
            }
            catch (EsentDatabaseFileReadOnlyException)
            {
                // Expected on Windows 10 20H1 and beyond.
            }
            catch (EsentPermissionDeniedException)
            {
                // Expected on Windows 10 19H2 and below.
            }
            finally
            {
                this.ReattachDatabase(AttachDatabaseGrbit.None);
            }
        }

        /// <summary>
        /// Test starting attach-time OLD2 with DefragmentSequentialBTrees on read-only database.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Start attach-time OLD2 with DefragmentSequentialBTrees on read-only database must not fail attach")]
        public void TestStartAttachTimeOld2WithDefragmentSequentialBTreesReadOnlyDbMustNotFail()
        {
            IntPtr defragParam = IntPtr.Zero;
            try
            {
                string dummy;
                Api.JetGetSystemParameter(this.instance, JET_SESID.Nil, Windows7Param.DefragmentSequentialBTrees, ref defragParam, out dummy, 0);
                Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, Windows7Param.DefragmentSequentialBTrees, 1, null);
                this.ReattachDatabase(AttachDatabaseGrbit.ReadOnly);
            }
            finally
            {
                Api.JetSetSystemParameter(this.instance, JET_SESID.Nil, Windows7Param.DefragmentSequentialBTrees, defragParam, null);
                this.ReattachDatabase(AttachDatabaseGrbit.None);
            }
        }

        /// <summary>
        /// Test starting and stopping OLD with JetDefragment2.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Start and stop online defragmentation with JetDefragment2")]
        public void TestStartAndStopJetDefragment2()
        {
            int passes = 1;
            int seconds = 1;
            Api.JetDefragment2(this.sesid, this.dbid, null, ref passes, ref seconds, null, DefragGrbit.BatchStart);
            Api.JetDefragment2(this.sesid, this.dbid, null, ref passes, ref seconds, null, DefragGrbit.BatchStop);
        }

        /// <summary>
        /// Verify JetDefragment2 calls the callback.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Verify JetDefragment2 calls the callback")]
        public void TestJetDefragment2Callback()
        {
#if !MANAGEDESENT_ON_CORECLR // The light ESENT doesn't support callbacks.
            ManualResetEvent oldFinishedEvent = new ManualResetEvent(false);
            JET_CALLBACK callback = (sesid, dbid, tableid, cbtyp, arg1, arg2, context, unused) =>
            {
                oldFinishedEvent.Set();
                return JET_err.Success;
            };

            int passes = 1;
            int seconds = 1;
            Api.JetDefragment2(this.sesid, this.dbid, null, ref passes, ref seconds, callback, DefragGrbit.BatchStart);
            Assert.IsTrue(
                oldFinishedEvent.WaitOne(TimeSpan.FromSeconds(10)),
                "Online Defragmentation Callback not called");
            Api.JetDefragment2(this.sesid, this.dbid, null, ref passes, ref seconds, null, DefragGrbit.BatchStop);

            // Don't let the callback be collected before OLD finishes.
            GC.KeepAlive(callback);
#endif // !MANAGEDESENT_ON_WSA
        }

        #endregion

        #region Callback Tests

        /// <summary>
        /// Register a callback and make sure it is called.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Register a callback and make sure it is called")]
        public void VerifyRegisteredCallbackIsCalled()
        {
            bool callbackWasCalled = false;
            JET_CALLBACK callback = (s, d, t, cbtyp, arg1, arg2, context, unused) =>
            {
                callbackWasCalled = true;
                return JET_err.Success;
            };

            JET_HANDLE callbackId;
            Api.JetRegisterCallback(this.sesid, this.tableid, JET_cbtyp.BeforeInsert, callback, IntPtr.Zero, out callbackId);

            using (var transaction = new Transaction(this.sesid))
            using (var update = new Update(this.sesid, this.tableid, JET_prep.Insert))
            {
                Api.SetColumn(this.sesid, this.tableid, this.columnidLongText, Any.String, Encoding.Unicode);
                update.Save();
                transaction.Commit(CommitTransactionGrbit.None);
            }

            Assert.IsTrue(callbackWasCalled, "callback was not called");
        }

        /// <summary>
        /// Unregister a callback and make sure it is not called.
        /// </summary>
        [TestMethod]
        [Priority(2)]
        [Description("Unregister a callback and make sure it is not called")]
        public void VerifyUnregisteredCallbackIsNotCalled()
        {
            bool callbackWasCalled = false;
            JET_CALLBACK callback = (s, d, t, cbtyp, arg1, arg2, context, unused) =>
            {
                callbackWasCalled = true;
                return JET_err.Success;
            };

            JET_HANDLE callbackId;
            Api.JetRegisterCallback(this.sesid, this.tableid, JET_cbtyp.BeforeInsert, callback, IntPtr.Zero, out callbackId);
            Api.JetUnregisterCallback(this.sesid, this.tableid, JET_cbtyp.BeforeInsert, callbackId);

            using (var transaction = new Transaction(this.sesid))
            using (var update = new Update(this.sesid, this.tableid, JET_prep.Insert))
            {
                Api.SetColumn(this.sesid, this.tableid, this.columnidLongText, Any.String, Encoding.Unicode);
                update.Save();
                transaction.Commit(CommitTransactionGrbit.None);
            }

            Assert.IsFalse(callbackWasCalled, "callback was called");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Update the cursor and goto the returned bookmark.
        /// </summary>
        private void UpdateAndGotoBookmark()
        {
            var bookmark = new byte[SystemParameters.BookmarkMost];
            int bookmarkSize;
            Api.JetUpdate(this.sesid, this.tableid, bookmark, bookmark.Length, out bookmarkSize);
            Api.JetGotoBookmark(this.sesid, this.tableid, bookmark, bookmarkSize);
        }

        /// <summary>
        /// Sets the LongText column in the table from a string. An update must be prepared.
        /// </summary>
        /// <param name="s">The string to set.</param>
        private void SetColumnFromString(string s)
        {
            byte[] data = Encoding.Unicode.GetBytes(s);
            Api.JetSetColumn(this.sesid, this.tableid, this.columnidLongText, data, data.Length, SetColumnGrbit.IntrinsicLV | Windows7Grbits.Uncompressed, null);
        }

        /// <summary>
        /// Sets the LongText column in the table from a string, using the Compressed bit. An update must be prepared.
        /// </summary>
        /// <param name="s">The string to set.</param>
        private void SetCompressedColumnFromString(string s)
        {
            byte[] data = Encoding.Unicode.GetBytes(s);
            Api.JetSetColumn(this.sesid, this.tableid, this.columnidLongText, data, data.Length, SetColumnGrbit.IntrinsicLV | Windows7Grbits.Compressed, null);
        }

        /// <summary>
        /// Returns the value in the LongText column as a string. The cursor must be on a record.
        /// </summary>
        /// <returns>The value of the LongText column as a string.</returns>
        private string RetrieveColumnAsString()
        {
            return Api.RetrieveColumnAsString(this.sesid, this.tableid, this.columnidLongText, Encoding.Unicode);
        }

        /// <summary>
        /// Reattaches the database.
        /// </summary>
        /// <param name="attachGrbit">The attach grbit.</param>
        private void ReattachDatabase(AttachDatabaseGrbit attachGrbit)
        {
            Api.JetCloseTable(this.sesid, this.tableid);
            Api.JetCloseDatabase(this.sesid, this.dbid, CloseDatabaseGrbit.None);
            Api.JetDetachDatabase(this.sesid, this.database);
            Api.JetAttachDatabase(this.sesid, this.database, attachGrbit);
            Api.JetOpenDatabase(this.sesid, this.database, null, out this.dbid, OpenDatabaseGrbit.None);
            Api.JetOpenTable(this.sesid, this.dbid, this.table, null, 0, OpenTableGrbit.None, out this.tableid);
        }

        #endregion HelperMethods
    }
}