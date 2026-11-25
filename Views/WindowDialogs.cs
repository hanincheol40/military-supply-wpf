using System.Windows;
using Microsoft.Win32;
using MilitarySupply.ViewModels;

namespace MilitarySupply.Views
{
    // IUserDialogs를 실제 WPF 창으로 구현한 것
    public sealed class WindowDialogs(Window owner) : IUserDialogs
    {
        private const string XlsxFilter = "Excel 통합 문서 (*.xlsx)|*.xlsx";

        public string? OpenWorkbook()
        {
            var dialog = new OpenFileDialog { Filter = XlsxFilter };
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        public string? SaveWorkbook()
        {
            var dialog = new SaveFileDialog
            {
                Filter = XlsxFilter,
                FileName = $"군수품_보급현황_{DateTime.Today:yyyyMMdd}.xlsx",
            };
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        public bool ConfirmImport(string path) =>
            MessageBox.Show(owner,
                $"다음 XLSX의 병사·재고·지급·요청 데이터로 전체 교체합니다.\n{path}\n\n현재 데이터는 data.json.bak에 백업됩니다. 계속할까요?",
                "가져오기 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public void ShowError(string message) =>
            MessageBox.Show(owner, message, "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
