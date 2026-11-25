using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MilitarySupply.ViewModels
{
    // 값이 바뀌면 화면에 알려 주는 공통 부모 클래스
    public class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Changed([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // 같은 값이면 그냥 두고, 다르면 저장한 뒤 알린다
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            Changed(name);
            return true;
        }
    }
}
