﻿// Copyright 2021 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using SwptSaveEditor.Dialogs;
using SwptSaveEditor.Undo;
using SwptSaveEditor.Utils;
using SwptSaveLib;
using SwptSaveLib.ValueTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace SwptSaveEditor.Document
{
    /// <summary>
    /// View model for SaveFile instances being displayed in the editor
    /// </summary>
    internal class SaveDocument : ViewModelBase
    {
        private readonly SaveFile mFile;

        private readonly UndoService mUndoService;

        private readonly DelegateCommand mUndoCommand;
        private readonly DelegateCommand mRedoCommand;
        private readonly DelegateCommand mSaveCommand;
        private readonly DelegateCommand mReloadCommand;

        private readonly DelegateCommand mClearFilterCommand;
        private readonly DelegateCommand mRenamePropertyCommand;
        private readonly DelegateCommand mCopyPropertyCommand;
        private readonly DelegateCommand mPastePropertyCommand;
        private readonly DelegateCommand mMovePropertyUpCommand;
        private readonly DelegateCommand mAddPropertyCommand;
        private readonly DelegateCommand mRemovePropertyCommand;
        private readonly DelegateCommand mMovePropertyDownCommand;

        public string Name => mFile.Name;

        public IReadOnlyList<SaveProperty> Properties => mFile.Properties;

        public int SelectedPropertyIndex
        {
            get => _selectedPropertyIndex;
            set
            {
                if (Set(ref _selectedPropertyIndex, value))
                {
                    mRenamePropertyCommand.RaiseCanExecuteChanged();
                    mCopyPropertyCommand.RaiseCanExecuteChanged();
                    mMovePropertyDownCommand.RaiseCanExecuteChanged();
                    mMovePropertyUpCommand.RaiseCanExecuteChanged();
                    mRemovePropertyCommand.RaiseCanExecuteChanged();
                }
            }
        }
        private int _selectedPropertyIndex;

        public ListSortDirection? NameSortDirection
        {
            get => _nameSortDirection;
            set
            {
                if (Set(ref _nameSortDirection, value))
                {
                    NotifyPropertyChanged(nameof(CanMoveProperties));
                    mMovePropertyDownCommand.RaiseCanExecuteChanged();
                    mMovePropertyUpCommand.RaiseCanExecuteChanged();
                }
            }
        }
        private ListSortDirection? _nameSortDirection = null;

        public ListSortDirection? TypeSortDirection
        {
            get => _typeSortDirection;
            set
            {
                if (Set(ref _typeSortDirection, value))
                {
                    NotifyPropertyChanged(nameof(CanMoveProperties));
                    mMovePropertyDownCommand.RaiseCanExecuteChanged();
                    mMovePropertyUpCommand.RaiseCanExecuteChanged();
                }
            }
        }
        private ListSortDirection? _typeSortDirection = null;

        public string PropertyFilter
        {
            get => _propertyFilter;
            set
            {
                if (Set(ref _propertyFilter, value))
                {
                    ICollectionView view = CollectionViewSource.GetDefaultView(mFile.Properties);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        view.Filter = null;
                    }
                    else
                    {
                        view.Filter = (obj) => ((SaveProperty)obj).Name.ToLowerInvariant().Contains(value.Trim().ToLowerInvariant());
                    }
                    NotifyPropertyChanged(nameof(CanMoveProperties));
                    mMovePropertyDownCommand.RaiseCanExecuteChanged();
                    mMovePropertyUpCommand.RaiseCanExecuteChanged();
                    mClearFilterCommand.RaiseCanExecuteChanged();
                }
            }
        }
        private string _propertyFilter;

        public bool CanMoveProperties => NameSortDirection == null && TypeSortDirection == null && string.IsNullOrWhiteSpace(PropertyFilter);

        public IInputElement FilterElement { get; set; }

        public IUndoService UndoService => mUndoService;

        public ICommand UndoCommand => mUndoCommand;

        public ICommand RedoCommand => mRedoCommand;

        public ICommand SaveCommand => mSaveCommand;

        public ICommand ReloadCommand => mReloadCommand;

        public ICommand ClearFilterCommand => mClearFilterCommand;

        public ICommand RenamePropertyCommand => mRenamePropertyCommand;

        public ICommand CopyPropertyCommand => mCopyPropertyCommand;

        public ICommand PastePropertyCommand => mPastePropertyCommand;

        public ICommand MovePropertyDownCommand => mMovePropertyDownCommand;

        public ICommand MovePropertyUpCommand => mMovePropertyUpCommand;

        public ICommand AddPropertyCommand => mAddPropertyCommand;

        public ICommand RemovePropertyCommand => mRemovePropertyCommand;

        public SaveDocument(IServiceProvider services, SaveFile file)
        {
            mFile = file;
            mUndoService = new UndoService();

            mUndoCommand = new DelegateCommand(mUndoService.Undo, () => mUndoService.CanUndo);
            mRedoCommand = new DelegateCommand(mUndoService.Redo, () => mUndoService.CanRedo);
            mSaveCommand = new DelegateCommand(Save, () => !mUndoService.IsSavePoint);
            mReloadCommand = new DelegateCommand(Reload);

            mClearFilterCommand = new DelegateCommand(ClearFilter, () => !string.IsNullOrEmpty(PropertyFilter));
            mRenamePropertyCommand = new DelegateCommand(RenameProperty, () => SelectedPropertyIndex >= 0);
            mCopyPropertyCommand = new DelegateCommand(CopyProperty, () => SelectedPropertyIndex >= 0);
            mPastePropertyCommand = new DelegateCommand(PasteProperty);
            mMovePropertyDownCommand = new DelegateCommand(MovePropertyDown, CanMovePropertyDown);
            mMovePropertyUpCommand = new DelegateCommand(MovePropertyUp, CanMovePropertyUp);
            mAddPropertyCommand = new DelegateCommand(AddProperty);
            mRemovePropertyCommand = new DelegateCommand(RemoveProperty, () => SelectedPropertyIndex >= 0);

            mUndoService.StateChanged += UndoService_StateChanged;
        }

        private void UndoService_StateChanged(object sender, EventArgs e)
        {
            mUndoCommand.RaiseCanExecuteChanged();
            mRedoCommand.RaiseCanExecuteChanged();
            mSaveCommand.RaiseCanExecuteChanged();
        }

        public void RecordValueEdit(SaveValue value, object oldData)
        {
            if (value.CompareData(oldData)) return;

            object newData = value.Data;
            DelegateUndoUnit unit = DelegateUndoUnit.Create(() => value.Data = newData, () => value.Data = oldData);
            mUndoService.PushUndoUnit(unit);
        }

        public void Save()
        {
            mFile.Save();
            mUndoService.SetSavePoint();
            mSaveCommand.RaiseCanExecuteChanged();
        }

        private void Reload()
        {
            if (!mUndoService.IsSavePoint)
            {
                MessageBoxResult result = MessageBox.Show(Application.Current.MainWindow, "Reloading from disk will discard unsaved changes and clear your undo history for this file. This action cannot be undone. Are you sure?", "Confirm Reload", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        break;
                    case MessageBoxResult.No:
                        return;
                }
            }
            else if (mUndoService.CanUndo || mUndoService.CanRedo)
            {
                MessageBoxResult result = MessageBox.Show(Application.Current.MainWindow, "Reloading from disk will clear your undo history for this file. Are you sure?", "Confirm Reload", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        break;
                    case MessageBoxResult.No:
                        return;
                }
            }
            mFile.Reload();
            mUndoService.Clear();
        }

        private void ClearFilter()
        {
            PropertyFilter = string.Empty;
        }

        private void RenameProperty()
        {
            SaveProperty property = GetSelectedProperty();
            string oldName = property.Name;

            RenamePropertyDialog dialog = new RenamePropertyDialog(oldName);
            if (dialog.ShowDialog(Application.Current.MainWindow) == true)
            {
                DelegateUndoUnit unit = DelegateUndoUnit.CreateAndExecute(
                    () => property.Name = dialog.PropertyName,
                    () => property.Name = oldName);

                mUndoService.PushUndoUnit(unit);
            }
        }

        private void CopyProperty()
        {
            SaveProperty property = GetSelectedProperty();

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                property.Save(writer);

                for (int tries = 3; tries > 0; --tries)
                {
                    try
                    {
                        Clipboard.SetData(DataFormats.Serializable, stream.ToArray());
                        tries = 0;
                    }
                    catch (ExternalException)
                    {
                        Thread.Sleep(1);
                    }
                }
            }
        }

        private void PasteProperty()
        {
            try
            {
                byte[] data = Clipboard.GetData(DataFormats.Serializable) as byte[];
                if (data != null)
                {
                    using (MemoryStream stream = new MemoryStream(data))
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        SaveProperty property = SaveProperty.Load(reader);

                        UndoGroup group = new UndoGroup();

                        int index = mFile.IndexOfProperty(property.Name);
                        if (index >= 0)
                        {
                            int nextIndex = mFile.IndexOfProperty(property.Name, index + 1);
                            if (nextIndex >= 0)
                            {
                                // More than one property already exists with the same name, so just add another
                                index = mFile.Properties.Count;
                            }
                            else
                            {
                                // Found a property with the same name. Replace it or add as new (prompt user)?
                                PastePropertyDialog dialog = new PastePropertyDialog(property.Name);
                                switch (dialog.ShowDialog(Application.Current.MainWindow))
                                {
                                    case PastePropertyDialogResult.Cancel:
                                        return;
                                    case PastePropertyDialogResult.Replace:
                                        {
                                            SaveProperty oldProperty = mFile.Properties[index];
                                            group.Add(DelegateUndoUnit.CreateAndExecute(
                                                () => mFile.RemoveProperty(index),
                                                () => mFile.InsertProperty(index, oldProperty)));
                                        }
                                        break;
                                    case PastePropertyDialogResult.AddNew:
                                        index = mFile.Properties.Count;
                                        break;
                                }
                            }
                        }
                        else
                        {
                            index = mFile.Properties.Count;
                        }

                        group.Add(DelegateUndoUnit.CreateAndExecute(
                            () => mFile.InsertProperty(index, property),
                            () => mFile.RemoveProperty(index)));

                        mUndoService.PushUndoUnit(group);
                    }
                }
            }
            catch
            {
            }
        }

        private void MovePropertyDown()
        {
            int index = SelectedPropertyIndex;

            DelegateUndoUnit unit = DelegateUndoUnit.CreateAndExecute(
                () => mFile.MovePropertyDown(index),
                () => mFile.MovePropertyUp(index + 1));

            mUndoService.PushUndoUnit(unit);
        }

        private void MovePropertyUp()
        {
            int index = SelectedPropertyIndex;

            DelegateUndoUnit unit = DelegateUndoUnit.CreateAndExecute(
                () => mFile.MovePropertyUp(index),
                () => mFile.MovePropertyDown(index - 1));

            mUndoService.PushUndoUnit(unit);
        }

        private bool CanExecutePropertyMove()
        {
            return CanMoveProperties && SelectedPropertyIndex >= 0;
        }

        private bool CanMovePropertyDown()
        {
            return CanExecutePropertyMove() && mFile.CanMovePropertyDown(SelectedPropertyIndex);
        }

        private bool CanMovePropertyUp()
        {
            return CanExecutePropertyMove() && mFile.CanMovePropertyUp(SelectedPropertyIndex);
        }

        private void AddProperty()
        {
            NewPropertyDialog dialog = new NewPropertyDialog();
            if (dialog.ShowDialog(Application.Current.MainWindow) == true)
            {
                SaveProperty property = new SaveProperty(dialog.PropertyName, dialog.PropertyIsArray ? new ArrayValue(dialog.PropertyType) : SaveValue.Create(dialog.PropertyType));
                int index = mFile.Properties.Count;

                DelegateUndoUnit unit = DelegateUndoUnit.CreateAndExecute(
                    () => mFile.AddProperty(property),
                    () => mFile.RemoveProperty(index));

                mUndoService.PushUndoUnit(unit);
            }
        }

        private void RemoveProperty()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(mFile.Properties);
            view.MoveCurrentToPosition(SelectedPropertyIndex);
            
            SaveProperty property = (SaveProperty)view.CurrentItem;
            int index = mFile.IndexOfProperty(property);

            DelegateUndoUnit unit = DelegateUndoUnit.CreateAndExecute(
                () =>
                {
                    view.MoveCurrentToNext();
                    mFile.RemoveProperty(index);
                    if (view.IsCurrentAfterLast) view.MoveCurrentToLast();
                    SelectedPropertyIndex = view.CurrentPosition;
                },
                () =>
                {
                    mFile.InsertProperty(index, property);
                    view.MoveCurrentTo(property);
                    SelectedPropertyIndex = view.CurrentPosition;
                });
            
            mUndoService.PushUndoUnit(unit);
        }

        private SaveProperty GetSelectedProperty()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(mFile.Properties);
            view.MoveCurrentToPosition(SelectedPropertyIndex);
            return (SaveProperty)view.CurrentItem;
        }
    }
}
