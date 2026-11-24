using System.Windows;
using MilitarySupply.Views;

namespace MilitarySupply
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // View 확인용 임시 데이터. ViewModel이 준비되면 MainViewModel로 교체합니다.
            DataContext = new PreviewData();
        }
    }
}
