using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TensometerPoltter.ViewModels.Base
{
    internal abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        protected virtual bool Set<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propName);
            return true;
        }

        //~BaseViewModel() 
        //{
        //    Dispose(false);
        //}

        //public void Dispose()
        //{
        //    Dispose(true);
        //}

        //private bool disposed;

        //protected virtual void Dispose(bool disposing) 
        //{
        //    if (!disposing || disposed) return;
        //    disposed = true;
        //    // Освобождение ресурсов
        //}
    }
}
