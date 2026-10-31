namespace WindowExe.Desktop.Desktop.Window
{
    // 세 번째 검색기록을 해독한 뒤 열리는 예시 창.
    // 실제 이미지/파일 화면으로 바꾸려면 WindowManager의 gun-file 프리팹만 교체하면 됩니다.
    public class GunFileWindow : NotepadWindow
    {
        protected override void InitializePrimary()
        {
            base.InitializePrimary();
            SetWindowTitle("총");
            SetText("[총 파일]\n\n해독된 검색기록에서 접근했습니다.\n이곳에 다음 퍼즐이나 파일 획득 기능을 추가하세요.");
        }
    }
}
