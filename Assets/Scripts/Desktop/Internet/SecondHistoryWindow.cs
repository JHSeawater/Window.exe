namespace WindowExe.Desktop.Desktop.Window
{
    // 검색기록 2 전용. 여기에만 두 번째 기록의 이벤트/퍼즐을 추가하면 됩니다.
    public class SecondHistoryWindow : NotepadWindow
    {
        protected override void InitializePrimary()
        {
            base.InitializePrimary();
            SetWindowTitle("삭제한 파일 복구하기");
            SetText("삭제된 파일은 언제나 완전히 사라지는 것은 아닙니다.\n\n" +
                    "휴지통, 백업 기록, 임시 저장 폴더를 확인하세요.\n\n" +
                    "이상한 이름으로 남아 있는 파일을 발견했다면...");
        }
    }
}
