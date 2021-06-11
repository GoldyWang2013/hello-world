
using System;
using Fx.Common.DataAccess;
using Fx.Common.Data;
using Fx.Common.Db;

using System.Data;
using Fx.Common.Filters;
using Fx.Common.Exceptions;
using System.ComponentModel;
using Fx.Common.Dicts;
using System.Text;
using Fx.Common.Meta;
using Fx.Common.Context;
using Fx.Common.Meta.Types;
using System.Reflection;
using Fx.Common.DataType;
using Fx.Common.Grid;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Fx.Common.Objects;
using Fx.Common.Log;
using Fx.Common.Trees;
using Fx.Common.AppEnv;
using System.Diagnostics;
using Fx.Common.Entity;
using Fx.Common.Tips;

namespace Fx.Common.Entity
{

    /// <summary>
    /// CEntity
    /// </summary>
    [DataContract]
    public partial class CEntity : CBaseEntity,
        IBaseEntity, INotifyPropertyChanged, IEntity, IEditableObject, IComparable // Component : ICloneable 
    {

        public virtual int CompareTo(object obj)
        {
            return 0;
        }
        // TODO:ConcurentCheckMode 
        //---------------------------------------------------------------------
        public virtual TPersistState PersistState { get; set; }
        public bool Inability
        {
            get { return PersistState == TPersistState.MarkDeleted || PersistState == TPersistState.Detached; }
        }
        public TConcurentCheckMode ConcurentCheckMode { get; set; }
        //---------------------------------------------------------------------
        static CEntity()
        {
        }
        //---------------------------------------------------------------------
        public static void RegisterClass(Type entityType, Type dataAccessType)
        {
            TRegisterInfo r = new TRegisterInfo();
            r.EntityType = entityType;
            r.DataAccessType = dataAccessType;
            TMetaRegistry.Default.RegisterEntityCore(r);
        }

        //---------------------------------------------------------------------
        public CEntity()
        {
            InitObject();
        }
        //---------------------------------------------------------------------
        public CEntity(CEntityContext ctx)
        {
            Context = ctx;
            InitObject();
        }
        //---------------------------------------------------------------------
        public CEntity(CEntity parent)
        {
            Parent = parent;
            InitObject();
        }
        //---------------------------------------------------------------------
        private void InitObject()
        {
            InitMembers();
            Id = CreateCoid();
            PersistState = TPersistState.Added;
            HasSelectedField = true;
            IsMarkDelete = false;
        TODO: RegisterValidators
            RegisterValidators();
        }
        //----------------------------------------------------------
        public virtual string CreateCoid()
        {
            string coidPrefix;
            TMetaEntity meta = GetMetaEntity(this.GetType());
            if (meta != null)
                coidPrefix = meta.MetaTable.CoidPrefix;
            else
                coidPrefix = String.Empty;

            return TCoid.CreateCoid(coidPrefix);
        }
        //----------------------------------------------------------
        public virtual void GetNewCoid()
        {
            Id = CreateCoid();
        }
        //----------------------------------------------------------
        public virtual void InitMembers()
        {
            TMetaEntity me = GetMetaEntity(this.GetType());
            if (me == null)
                return;

            TMetaTable mt = me.MetaTable;
            foreach (TMetaColumn mc in mt.FullValueColumns)
            {
                TDataType dataType = mc.DataType;
                if (dataType == TDataType.DateTime || dataType == TDataType.Date)
                {
                    object nullValue = TConvert.GetNullValue(dataType);
                    this.SetEpoPorpertyValue(mc, nullValue);
                }
            }
        }
        //---------------------------------------------------------------------
        public virtual void SetDefaultValue()
        {
        }
        //----------------------------------------------------------
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        //---------------------------------------------------------
        public override string ToString()
        {
            return Id;
        }
        //---------------------------------------------------------
        protected CDataAccess m_DataAccess;
        public virtual CDataAccess DataAccess
        {
            get
            {
                //TODO: find is there any override CreateDataAccess method?
                if (m_DataAccess != null)
                    return m_DataAccess;

                m_DataAccess = GetDataAccess();
                return m_DataAccess;
            }
            set { m_DataAccess = value; }
        }

        //---------------------------------------------------------
        public virtual CDataAccess GetDataAccess()
        {
            if (Context == null)
                return null;

            Type type = this.GetType();
            TMetaEntity meta = GetMetaEntity(type);

            CDataAccess access = Context.AccessCache.GetDataAccess(meta);
            if (access == null)
            {
                //TODO: DataAccess comes first from override method, then MetaEntity
                // find is there any override CreateDataAccess in exactly that hierarchy level?
                access = CreateDataAccess();
                access.MetaTable = meta.MetaTable;
                Context.AccessCache.Register(meta, access);
            }
            access.Context = this.Context;
            return access;
        }
        //---------------------------------------------------------
        public virtual CDataAccess CreateDataAccess()
        {
            //TODO: find is there any override CreateDataAccess
            return new CDataAccess(Context);
        }
        //---------------------------------------------------------
        public virtual void Load()
        {
            throw new Exception("The method  is not implemented.");
        }
        //----------------------------------------------------------
        public virtual void Load(Guid guid)
        {
            throw new Exception("The method  is not implemented.");
        }
        //----------------------------------------------------------
        public virtual void Load(int no)
        {
            //throw new Exception("The method  is not implemented.");
            BeforeLoad();
            Type t2 = this.GetType();
            while (t2 != typeof(CEntity))
            {
                TMetaEntity me2 = GetMetaEntity(t2);
                TMetaTable mt2 = me2.MetaTable;
                TInheritMappingType inheritType = mt2.InheritMappingType;

                ///单表继承--单表存贮 
                if (inheritType == TInheritMappingType.TablePerConcreteClass)
                {
                    Load(no, me2);
                    if (IsBakMode)
                        AcceptChanges(me2);
                    break;
                }
                /// 多表继承--分表存贮
                else if (inheritType == TInheritMappingType.TablePerSubClass)
                {
                    Load(no, me2);
                    if (IsBakMode)
                        AcceptChanges(me2);
                    t2 = t2.BaseType;
                }
            }
            PersistState = TPersistState.Opened;
            AfterLoad();
        }
        //----------------------------------------------------------
        public void Load(int no, TMetaEntity me)
        {
            CDataAccess access = Context.GetDataAccess(me);
            access.Context = this.Context;
            DataTable dataTable = access.SelectByNo(no);
            Load(dataTable, me);
        }
        ////----------------------------------------------------------
        // 不支持继承
        //public virtual void Load(string id)
        //{
        //    DataTable dataTable = DataAccess.SelectById(id);
        //    Load(dataTable);
        //}
        //----------------------------------------------------------
        // 支持继承
        public virtual void Load(string id)
        {
            BeforeLoad();
            Type t2 = this.GetType();
            while (t2 != typeof(CEntity))
            {
                TMetaEntity me2 = GetMetaEntity(t2);
                TMetaTable mt2 = me2.MetaTable;
                TInheritMappingType inheritType = mt2.InheritMappingType;

                ///单表继承--单表存贮 
                if (inheritType == TInheritMappingType.TablePerConcreteClass)
                {
                    Load(id, me2);
                    if (IsBakMode)
                        AcceptChanges(me2);

                    break;
                }
                /// 多表继承--分表存贮
                else if (inheritType == TInheritMappingType.TablePerSubClass)
                {
                    Load(id, me2);
                    if (IsBakMode)
                        AcceptChanges(me2);
                    t2 = t2.BaseType;
                }
            }
            PersistState = TPersistState.Opened;
            AfterLoad();
        }
        //----------------------------------------------------------
        public void Load(string id, TMetaEntity me)
        {
            CDataAccess access = Context.GetDataAccess(me);
            access.Context = this.Context;
            DataTable dataTable = access.SelectById(id);
            Load(dataTable, me);
        }
        //----------------------------------------------------------
        private void Load(DataTable table, TMetaEntity me)
        {
            TMetaTable mt = me.MetaTable;
            if (table.Rows.Count == 0)
            {
                if (mt.InheritMappingType == TInheritMappingType.TablePerConcreteClass)
                    throw new TFetchNoneException(this.GetType().ToString() + " Fetch None");
            }

            if (table.Rows.Count == 1)
            {
                Load(table.Rows[0], me);
            }
            else if (table.Rows.Count > 1)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < table.Rows.Count; i++)
                    sb.AppendLine(table.Rows[i]["ID"].ToString());
                throw new TFetchMoreException(String.Format("{0}--Fetch More than one row{1}{2}", GetType(), Environment.NewLine, sb));
            }
        }
        //----------------------------------------------------------
        private void Load(DataRow row, TMetaEntity me)
        {
            Pull(this, row, me);
            //if (IsBakMode)
            //    AcceptChanges();
        }
        //---------------------------------------------------------------------
        public virtual void Pull(CEntity e, DataRow row, TMetaEntity me)
        {
            TInheritMappingType inheritType = me.MetaTable.InheritMappingType;
            if (inheritType == TInheritMappingType.TablePerConcreteClass)
            {
                foreach (TMetaColumn mc in me.MetaTable.FullValueColumns)
                    Pull(e, row, mc);
            }
            else if (inheritType == TInheritMappingType.TablePerSubClass)
            {
                foreach (TMetaColumn mc in me.MetaTable.ValueColumnsWithId)
                    Pull(e, row, mc);
            }
        }
        //----------------------------------------------------------
        public virtual void Load(TFilterCollection filters)
        {
            DataTable dataTable = DataAccess.Select(filters);
            Load(dataTable);
        }
        //----------------------------------------------------------
        public virtual void Load(DataTable tbl)
        {
            BeforeLoad();
            if (tbl.Rows.Count == 0)
            {
                //SimpleLog.Write(this.GetType().ToString() + " Fetch None");
                throw new TFetchNoneException(this.GetType().ToString() + " Fetch None");
            }
            else if (tbl.Rows.Count == 1)
                Load(tbl.Rows[0]);

            else if (tbl.Rows.Count > 1)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < tbl.Rows.Count; i++)
                {
                    var column = tbl.Columns["ID"];
                    if (column != null)
                        sb.AppendLine(tbl.Rows[i][column].ToString());
                }
                string msg = sb.ToString();
                //SimpleLog.Write(String.Format("{0}----Fetch More than one row{1}{2}", GetType(), Environment.NewLine, msg));
                throw new TFetchMoreException(String.Format("{0}----Fetch More than one row{1}{2}", GetType(), Environment.NewLine, msg));
            }
        }
        //---------------------------------------------------------------------
        public virtual void Load(CEntity entity)
        {
            throw new NotImplementedException();
        }
        //----------------------------------------------------------
        public virtual void BeforeLoad()
        {
        }
        //----------------------------------------------------------
        public virtual void AfterLoad()
        {
        }
        //----------------------------------------------------------
        public virtual void Load(DataRow row)
        {
            Pull(this, row);
            PersistState = TPersistState.Opened;

            if (IsBakMode)
                AcceptChanges();

            AfterLoad();
        }
        //---------------------------------------------------------------------
        public virtual void Pull(CEntity e, DataRow row)
        {
            TMetaEntity meta = GetMetaEntity(this.GetType());
            foreach (TMetaColumn mc in meta.MetaTable.FullValueColumns)
            {
                if (!row.Table.Columns.Contains(mc.ColumnName))
                    continue;
                Pull(e, row, mc);
            }
        }
        //---------------------------------------------------------------------
        public virtual void Pull(CEntity ety, DataRow row, TMetaColumn mc)
        {
            string columnName = mc.ColumnName;
            //if(columnName=="A1623") {
            //    Debug.WriteLine("XXX");
            //}
            object v0 = row[columnName];

            //TODO: how to know nullable property
            /// System.Nullable     int? No;
            if (mc.IsGenericType)
            {
                if (v0 == DBNull.Value)
                    ety.SetEpoPorpertyValue(mc, null);
                else
                    ety.SetEpoPorpertyValue(mc, v0);
                return;
            }

            object customNullValue = mc.MappingAttribute.NullValue;
            if (customNullValue == null)
            {
                object v1 = TConvert.DbValue2DataValue(mc.DataType, v0);    /// most hit here!
                ety.SetEpoPorpertyValue(mc, v1);
                return;
            }
            /// CustomNullValue
            //TODO:  customNullValue!=null issues, not finished....
            Type underlyingType = mc.UnderlyingType;
            if (customNullValue.GetType() != underlyingType)
            {
                if (underlyingType == typeof(Decimal))
                {
                    // float to decimal string to decimal
                    Decimal d = Convert.ToDecimal(customNullValue);
                    object v2 = TConvert.DbValue2DataValue(underlyingType, row[columnName], d);
                    ety.SetEpoPorpertyValue(mc, v2);
                }
                else
                    throw new TException("nullValue.GetType() differ value.GetType");
            }
            else
            {
                object v3 = TConvert.DbValue2DataValue(underlyingType, row[columnName], customNullValue);
                ety.SetEpoPorpertyValue(mc, v3);
            }
        }
        //----------------------------------------------------------
        public virtual void InitReSubmit()
        {
            PersistState = TPersistState.Added;
        }
        //----------------------------------------------------------
        public virtual void BeforeSave()
        {
            ///******
        }
        //----------------------------------------------------------
        public virtual void AfterSave()
        {
            // Fire Event
            IBindingList bList = this.Owner;
            CEntityCollection entities = bList as CEntityCollection;
            if (entities != null)
            {
                var arg = new TEntityChangedEventArgs();
                arg.Entity = this;
                //rxArgs.EntityChangeType = TEntityChangeType.Add;
                entities.OnEntityChanged(this, arg);
            }
        }
        //----------------------------------------------------------
        public virtual void Save()
        {
            if (Context == null)
                throw new TException("Context is null");
            BeforeSave();
            SaveCore();
            AfterSave();
        }
        //----------------------------------------------------------
        private void SaveCore()
        {
            if (PersistState == TPersistState.OpenMemory)
                PersistState = TPersistState.Added;

            TMetaEntity meta = GetMetaEntity(this.GetType());
            TInheritMappingType inheritType = meta.MetaTable.InheritMappingType;

            // PerConcrete与PerSubClass同样都调用此函数Save(meta)
            // 在此函数中，去进一步区分MappingType
            // NG

            if (inheritType == TInheritMappingType.TablePerConcreteClass)
                SaveTablePerConcreteClass(meta);

            else if (inheritType == TInheritMappingType.TablePerSubClass)
                SaveTablePerSubClass();


            if (PersistState == TPersistState.Added)
                PersistState = TPersistState.Opened;

            else if (PersistState == TPersistState.Opened)
                PersistState = TPersistState.Opened;

            else if (PersistState == TPersistState.Modified)
                PersistState = TPersistState.Opened;


            else if (PersistState == TPersistState.MarkDeleted)
                PersistState = TPersistState.Detached;


            IsMarkDelete = false;
            //SetDirty(false);
            m_Dirty = TBoolEx.Null;
        }
        //----------------------------------------------------------
        // 同表储存
        private void SaveTablePerConcreteClass(TMetaEntity meta)
        {
            try
            {
                // PerConcrete与PerSubClass同样都调用函数Save(meta)
                // 在此函数中，去进一步区分MappingType
                // NG
                Save(meta);
            }
            catch (TDataSilentlyChangedException e1)
            {
                string trace = TEntityConcurrencyTrace.Trace(this, meta);
                SimpleLog.Write(trace);
                SimpleLog.Write(e1);
                throw e1;
            }
        }
        //----------------------------------------------------------
        // 分表储存
        private void SaveTablePerSubClass()
        {
            Type t2 = this.GetType();
            while (t2 != typeof(CEntity))
            {
                TMetaEntity meta2 = GetMetaEntity(t2);
                TMetaTable mt2 = meta2.MetaTable;
                TInheritMappingType inheritType = mt2.InheritMappingType;
                try
                {
                    // PerConcrete与PerSubClass同样都调用函数Save(meta)
                    // 在此函数中，去进一步区分MappingType
                    // NG
                    Save(meta2);
                }
                catch (TDataSilentlyChangedException e2)
                {
                    string trace = TEntityConcurrencyTrace.Trace(this, meta2);
                    SimpleLog.Write(trace);
                    SimpleLog.Write(e2);
                    throw e2;
                }
                t2 = t2.BaseType;
            }
        }
        //----------------------------------------------------------
        public void Save(TMetaEntity me)
        {
            // PerConcrete与PerSubClass同样都调用函数Save(meta)
            // 在此函数中，去进一步区分MappingType
            // NG

            /// 注意，Meta分级存贮。分表时要补Id

            TracSave(me);
            CDataAccess access = Context.GetDataAccess(me);
            access.Context = this.Context;

            TMetaTable mt = me.MetaTable;
            TDataTable table = Context.CreateDataTable(mt);
            TDataRow row = table.CreateDataRow();

            if (PersistState == TPersistState.Added)
            {
                //FillRow(me, row, this);
                this.Push(row, me);
                table.Rows.Add(row);
                access.InsertRow(table);
                AcceptChanges(me);
            }
            else if (PersistState == TPersistState.Opened || PersistState == TPersistState.Modified)
            {
                if (me.MetaTable.InheritMappingType == TInheritMappingType.TablePerSubClass)
                {
                    if (!access.ExistId(Id))
                    {
                        //FillRow(me, row, this);
                        this.Push(row, me);
                        table.Rows.Add(row);
                        access.InsertRow(table);
                        AcceptChanges(me);
                        return;
                    }
                }

                if (IsBakMode)
                {
                    //FillRow(me, row, this.Bak1);
                    this.Bak1.Push(row, me);
                    table.Rows.Add(row);
                    row.AcceptChanges();
                }

                //FillRow(me, row, this);
                this.Push(row, me);
                access.EditRow(table);

                SaveColumnsBrutally(me);    ///

                AcceptChanges(me);
            }
            else if (PersistState == TPersistState.MarkDeleted)
            {
                //FillRow(me, row, this.Bak1);
                this.Bak1.Push(row, me);
                table.Rows.Add(row);
                row.AcceptChanges();
                row.Delete();
                access.Delete(table);
                AcceptChanges(me);
            }
        }
        //---------------------------------------------------------------------
        private void SaveColumnsBrutally(TMetaEntity me)
        {
            TMetaTable mt = me.MetaTable;
            CDataAccess access = Context.GetDataAccess(me);
            access.Context = this.Context;
            string tableName = mt.TableName;
            foreach (TMetaColumn mc in mt.Columns)
            {
                if (!mc.IsBrutallySave)
                    continue;

                object v = GetEpoPropertyValue(mc);
                string strValue = null;
                switch (mc.DataType)
                {
                    case TDataType.Int32:
                    case TDataType.Int64:
                    case TDataType.Int16:
                    case TDataType.Decimal:
                        strValue = v.ToString();
                        break;

                    case TDataType.String:
                        strValue = String.Format("'{0}'", v);
                        break;

                    case TDataType.DateTime:
                        strValue = String.Format("'{0}'", v);
                        break;

                    default:
                        strValue = v.ToString();
                        break;
                }
                string cmdText = String.Format("Update {0} set {1}={2} where ID='{3}'", mt.TableName, mc.ColumnName, strValue, Id);
                access.ExecuteNonQuery(cmdText);
            }
        }
        //---------------------------------------------------------------------
        public virtual void Push(DataRow row, TMetaEntity me)
        {
            TMetaTable mt = me.MetaTable;
            TInheritMappingType inheritType = me.MetaTable.InheritMappingType;
            if (inheritType == TInheritMappingType.TablePerConcreteClass)
            {
                Push(row, mt.FullValueColumns);
            }
            else if (inheritType == TInheritMappingType.TablePerSubClass)
            {
                Push(row, mt.ValueColumns);
            }
        }
        //---------------------------------------------------------------------
        public virtual void Push(DataRow row, TMetaColumnCollection metaColumns)
        {
            foreach (TMetaColumn mc in metaColumns)
                this.Push(row, mc);
        }
        //---------------------------------------------------------------------
        public virtual void Push(DataRow row)
        {
            TMetaEntity me = GetMetaEntity(this.GetType());
            //FillRow(me, row, e);
            this.Push(row, me);
        }
        //---------------------------------------------------------------------
        public virtual void Push(DataRow row, TMetaColumn mc)
        {
            if (mc.IsNotSave || mc.IsBrutallySave)
                return;

            string columnName = mc.ColumnName;
            //if(columnName=="A1623") {
            //    Debug.WriteLine("Xxx");
            //}
            DataColumn dataColumn = row.Table.Columns[columnName];
            if (dataColumn == null)
                throw new TException(this.GetType().FullName + " Column-'" + columnName + "' Missing");

            var dbValue = this.GetDbValue(mc);
            row[columnName] = dbValue;
        }
        //--------------------------------------------------------
        public void Fill(TMetaColumnCollection keyColumns)
        {
            foreach (TMetaColumn mc in keyColumns)
            {
                var dbValue = GetDbValue(mc);
                mc.DbValue = dbValue;
                mc.DataValue = GetEpoPropertyValue(mc);
            }
        }
        //---------------------------------------------------------------------
        public virtual object GetDbValue(TMetaColumn mc)
        {
            object dataValue = this.GetEpoPropertyValue(mc);
            if (mc.IsGenericType)
            {
                if (dataValue == null)
                    return DBNull.Value;
                else
                    return dataValue;
            }
            Type dotNetType = mc.UnderlyingType;
            object customNullValue = mc.MappingAttribute.NullValue;
            if (customNullValue == null)
            {
                return TConvert.DataValue2DbValue(mc.DataType, dataValue);
            }

            /// CustomNullValue
            if (customNullValue.GetType() != dotNetType)
            {
                if (dotNetType == typeof(Decimal))
                {          /// float to decimal
                    Decimal decima = Convert.ToDecimal(customNullValue);
                    return TConvert.DataValue2DbValue(dataValue, decima);
                }
                else
                    throw new TException("nullValue.GetType() differ value.GetType");
            }
            else
                return TConvert.DataValue2DbValue(dataValue, customNullValue);
        }
        //---------------------------------------------------------------------
        public bool IsMarkDelete { get; set; }
        public virtual void MarkDelete()
        {
            if (PersistState == TPersistState.Added)
                PersistState = TPersistState.Detached;

            else if (PersistState == TPersistState.Opened)
                PersistState = TPersistState.MarkDeleted;

            //Selected = false;
            IsMarkDelete = true;
        }
        //---------------------------------------------------------------------
        public virtual void UnMarkDelete()
        {
            if (PersistState == TPersistState.Detached)
                PersistState = TPersistState.Added;

            else if (PersistState == TPersistState.MarkDeleted)
                PersistState = TPersistState.Opened;

            IsMarkDelete = false;
        }
        //---------------------------------------------------------------------
        public void Detach()
        {
            throw new NotImplementedException();
        }
        //---------------------------------------------------------------------
        public virtual void Delete()
        {
            DataAccess.DeleteById(Id);
        }
        public virtual void DeepDelete()
        {
            throw new NotImplementedException();
        }
        //----------------------------------------------------------
        public override void Assign(object rhs)
        {
            CEntity that = rhs as CEntity;
            TMetaEntity meta = GetMetaEntity(this.GetType());
            foreach (TMetaColumn mc in meta.MetaTable.FullValueColumns)
            {
                //if (mc.ColumnName == "CAPTIONS_CN")
                //    System.Diagnostics.Debug.WriteLine(mc.ColumnName);

                object v = that.GetEpoPropertyValue(mc);
                this.SetEpoPorpertyValue(mc, v);
            }
        }
        //--------------------------------------------------------------
        // TODO: Distinguish between Equals/Equal
        public override bool Equals(object rhs)
        {
            if (rhs == null)
                return false;

            if (object.ReferenceEquals(this, rhs))
                return true;

            if (this.GetType() != rhs.GetType())
                return false;

            CEntity that = rhs as CEntity;
            if (that == null)
                return false;


            TMetaEntity me = GetMetaEntity(this.GetType());
            if (me == null)
                return base.Equals(rhs);

            bool b = true;
            foreach (TMetaColumn mc in me.MetaTable.FullValueColumns)
            {
                //if (!cm.IsCompare)
                //    continue;

                object v1 = this.GetEpoPropertyValue(mc);
                object v2 = that.GetEpoPropertyValue(mc);
                b = b && (object.Equals(v1, v2));
            }

            //b = b && (PersistState == that.PersistState);

            return b;
        }
        //----------------------------------------------------------
        public virtual void Assign(TMetaEntity me, CEntity rhs)
        {
            TMetaTable mt = me.MetaTable;

            TInheritMappingType inheritType = mt.InheritMappingType;
            TMetaColumnCollection mcs = null;
            if (inheritType == TInheritMappingType.TablePerConcreteClass)
                mcs = mt.FullValueColumns;
            else
                if (inheritType == TInheritMappingType.TablePerSubClass)
                mcs = mt.ValueColumns;

            CEntity that = rhs as CEntity;
            foreach (TMetaColumn mc in mcs)
            {
                //if (!cm.IsAssign)
                //    continue;
                if (mc.MemberName == "Captions.Cn (Assign)")
                    System.Diagnostics.Debug.WriteLine(mc.MemberName);

                object v2 = that.GetEpoPropertyValue(mc);
                this.SetEpoPorpertyValue(mc, v2);
            }
        }
        //----------------------------------------------------------
        public virtual void Assign(Type t, CEntity rhs)
        {
            TMetaEntity me = TMetaRegistry.Default.GetMeta(t);
            Assign(me, rhs);
        }
        //--------------------------------------------------------------
        public bool Equals(Type t, object rhs)
        {
            if (rhs == null)
                return false;

            if (object.ReferenceEquals(this, rhs))
                return true;

            if (this.GetType() != rhs.GetType())
                return false;

            CEntity that = rhs as CEntity;
            if (that == null)
                return false;

            TMetaEntity meta = TMetaRegistry.Default.GetMeta(t);
            TMetaTable mt = meta.MetaTable;
            TInheritMappingType inheritType = mt.InheritMappingType;
            TMetaColumnCollection mcs = null;
            if (inheritType == TInheritMappingType.TablePerConcreteClass)
                mcs = mt.FullValueColumns;
            else if (inheritType == TInheritMappingType.TablePerSubClass)
                mcs = mt.ValueColumns;
            bool b = true;
            foreach (TMetaColumn mc in mcs)
            {
                //if (!cm.IsCompare)
                //    continue;
                if (mc.MemberName == "Captions.Cn (Equal)")
                    System.Diagnostics.Debug.WriteLine(mc.MemberName);
                object v1 = this.GetEpoPropertyValue(mc);
                object v2 = that.GetEpoPropertyValue(mc);
                b = b && (object.Equals(v1, v2));
            }
            b = b && (PersistState == that.PersistState);
            return b;
        }
        //----------------------------------------------------------
        public virtual void DeepLoad()
        {
            throw new Exception("The method  is not implemented.");
        }
        //----------------------------------------------------------
        public virtual void DeepLoad(string id)
        {
            throw new Exception("The method  is not implemented.");
        }
        //----------------------------------------------------------
        public virtual void DeepLoad(DataTable dataTable)
        {
            throw new Exception("The method  is not implemented.");
        }
        //---------------------------------------------------------------------
        public virtual void DeepLoad(DataRow row)
        {
            throw new Exception("The method  is not implemented.");
        }
        //-------------------------------------------------------------------
        public virtual void LazyLoad()
        {
        }
        //----------------------------------------------------------
        public virtual void DeepSave()
        {
            throw new Exception("The method  is not implemented.");
        }
        //-------------------------------------------------------------------
        public virtual void LazySave()
        {
        }
    }
}
////---------------------------------------------------------------------
//public virtual void FillColumn0(TMetaColumn mc, DataRow row, TEntity entity) {
//    if (mc.IsNotSave || mc.IsBrutallySave)
//        return;

//    string columnName = mc.ColumnName;
//    DataColumn dataColumn = row.Table.Columns[columnName];
//    if (dataColumn == null)
//        throw new TException(this.GetType().FullName + " Column-'" + columnName + "' Missing");


//    //PropertyInfo pi = mc.PropertyInfo;
//    object dataValue = entity.GetEpoPropertyValue(mc);

//    if (mc.IsGenericType) {
//        if (dataValue == null)
//            row[columnName] = DBNull.Value;
//        else
//            row[columnName] = dataValue;
//        return;
//    }

//    Type dotNetType = mc.UnderlyingType;
//    object customNullValue = mc.MappingAttribute.CustomNullValue;

//    if (customNullValue == null) {
//        row[columnName] = TConvert.DataValue2DbValue(mc.DataType, dataValue);
//        return;
//    }

//    /// customNullValue
//    if (customNullValue.GetType() != dotNetType) {
//        /// float to decimal
//        if (dotNetType == typeof(Decimal)) {
//            Decimal decima = Convert.ToDecimal(customNullValue);
//            row[columnName] = TConvert.DataValue2DbValue(dataValue, decima);
//        }
//        else
//            throw new TException("nullValue.GetType() differ value.GetType");
//    }
//    else
//        row[columnName] = TConvert.DataValue2DbValue(dataValue, customNullValue);
//}


////----------------------------------------------------------
//private void SaveCore0()
//{
//    Type t2 = this.GetType();
//    while (t2 != typeof(TEntity))
//    {
//        TMetaEntity meta2 = GetMetaEntity(t2);
//        TMetaTable mt2 = meta2.MetaTable;
//        TInheritMappingType inheritType = mt2.InheritMappingType;
//        if (inheritType == TInheritMappingType.TablePerConcreteClass)
//        {
//            try
//            {
//                Save(meta2);
//            }
//            catch (TDataSilentlyChangedException e1)
//            {
//                string trace = TEntityConcurrencyTrace.Trace(this, meta2);
//                SimpleLog.Write(trace);
//                SimpleLog.Write(e1);
//                throw e1;
//            }
//            break;
//        }
//        else if (inheritType == TInheritMappingType.TablePerSubClass)
//        {
//            try
//            {
//               Save(meta2);
//            }
//            catch (TDataSilentlyChangedException e2)
//            {
//                string trace = TEntityConcurrencyTrace.Trace(this, meta2);
//                SimpleLog.Write(trace);
//                SimpleLog.Write(e2);
//                throw e2;
//            }
//            t2 = t2.BaseType;
//        }
//    }

//    if (PersistState == TPersistState.Add)
//        PersistState = TPersistState.Opened;

//    else if (PersistState == TPersistState.Opened)
//        PersistState = TPersistState.Opened;

//    else if (PersistState == TPersistState.Modified)
//        PersistState = TPersistState.Opened;

//    else if (PersistState == TPersistState.MarkDeleted)
//        PersistState = TPersistState.Detached;

//    IsMarkDelete = false;

//    // Fire Event
//    //if (Owner != null)
//    //{
//    //    TEntityCollection entities = Owner as TEntityCollection)
//    //    if(entities != null)
//    //    {
//    //        TEntityChangedEventArgs rxArgs = new TEntityChangedEventArgs();
//    //        rxArgs.EntityChangeType = this.GetEntityChangeType();
//    //        rxArgs.Entity = this;
//    //        entities.OnEntityChanged(this, rxArgs);
//    //    }
//    //}
//}

////---------------------------------------------------------------------
//public virtual void FillRow(DataRow row, TEntity e) {
//    TMetaEntity me = GetMetaEntity(this.GetType());
//    //FillRow(me, row, e);
//    e.Push(row, me);
//}
////---------------------------------------------------------------------
//public virtual void FillColumn(TMetaColumn mc, DataRow row , TEntity entity) {
//    if (mc.IsNotSave || mc.IsBrutallySave)
//        return;

//    string columnName = mc.ColumnName;
//    DataColumn dataColumn = row.Table.Columns[columnName];
//    if (dataColumn == null)
//        throw new TException(this.GetType().FullName + " Column-'" + columnName + "' Missing");

//    var dbValue = entity.GetDbValue(mc);
//    row[columnName] = dbValue;
//}
////---------------------------------------------------------------------
//public virtual void FillRow(TMetaEntity me, DataRow row, TEntity e) {
//    if (e == null)
//        return;

//    TMetaTable mt = me.MetaTable;
//    TInheritMappingType inheritType = me.MetaTable.InheritMappingType;
//    if (inheritType == TInheritMappingType.TablePerConcreteClass) {
//        foreach (TMetaColumn mc in mt.FullValueColumns)
//            //FillColumn(mc, row, e);
//            e.Push(row, mc);
//    }
//    else if (inheritType == TInheritMappingType.TablePerSubClass) {
//        foreach (TMetaColumn mc in mt.ValueColumns)
//            //FillColumn(mc, row,e);
//            e.Push(row, mc);

//        //if (!(me.EntityType == typeof(TEntity))) {
//        //    /// ID?
//        //    TMetaColumn metaId = TMetaRegistry.Default.MetaId;
//        //    FillColumn(metaId, row, e);
//        //}
//    }
//}
