namespace MilitarySupply.ViewModels
{
    // ViewModel이 파일 창이나 메시지 창을 직접 띄우지 않도록 따로 뺀 것
    public interface IUserDialogs
    {
        string? OpenWorkbook();
        string? SaveWorkbook();
        bool ConfirmImport(string path);
        void ShowError(string message);
    }
}
