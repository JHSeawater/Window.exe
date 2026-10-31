namespace WindowExe.Desktop.Desktop.Window
{
    // 검색기록 1 전용. 여기에만 첫 번째 기록의 이벤트/퍼즐을 추가하면 됩니다.
    public class FirstHistoryWindow : NotepadWindow
    {
        protected override void InitializePrimary()
        {
            base.InitializePrimary();
            SetWindowTitle("낡은 컴퓨터 수리 방법");
            SetText("오래된 컴퓨터가 켜지지 않나요?\n\n" +
                    "1. 전원 케이블과 본체 전원을 확인하세요.\n" +
                    "2. 잠시 기다렸다가 다시 시작해 보세요.\n" +
                    "3. 알 수 없는 실행 파일은 주의하세요.");
        }
    }
}
