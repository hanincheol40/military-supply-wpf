using System.IO;
using System.Windows;
using MilitarySupply.Services;
using MilitarySupply.ViewModels;
using MilitarySupply.Views;

namespace MilitarySupply
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // 실행 파일 옆 Data 폴더에 저장한다 (처음이면 데모 데이터로 만든다)
                var path = Path.Combine(AppContext.BaseDirectory, "Data", "data.json");
                var service = new SupplyService(path);
                service.Save();
                DataContext = new MainViewModel(service, new WindowDialogs(this));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "데이터를 열 수 없습니다. 원본 파일을 보존합니다.\n" + ex.Message, "시작 오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }
    }
}
