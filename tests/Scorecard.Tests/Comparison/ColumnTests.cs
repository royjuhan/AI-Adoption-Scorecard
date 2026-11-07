namespace Scorecard.Tests.Comparison;

// 스펙 4.7 열(승인 질문 6). 카드 수용 기준 2(형식 오류: 열 없음).
// 키 열·compare 필드 열이 없으면 기준·결과 모두 column 오류. 쓰는 열이 둘이면 duplicate-column.
// 쓰지 않는 열은 무시(중복이어도). 머리 행 이름은 앞뒤 공백을 떼고 대소문자는 구분한다.
[Trait("Spec", "metrics")]
public class ColumnTests
{
    private const string OneMatch = "ref=1 cand=1 match=1 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0";
    private const string Good = "id,n,t\nk1,1,a\n";

    private static readonly Scorecard.Core.Cases.CaseDefinition Definition = CompareInput.Case(CompareInput.NumberAndTextCase);

    // 결과 자료에 compare 필드 열이 없으면 오류다(모든 항목을 값 불일치로 세지 않는다). 위치는 머리 행 줄, 경로는 열 이름.
    [Fact]
    public void Missing_column_in_candidate_is_an_error()
    {
        CompareInput.AssertErrors(CompareInput.Run(Definition, Good, "id,n\nk1,1\n"), "candidate/a.csv:1: column t: 머리 행에 열이 없다");
        CompareInput.AssertErrors(CompareInput.Run(Definition, Good, "key,n,t\nk1,1,a\n"), "candidate/a.csv:1: column id: 머리 행에 열이 없다");
    }

    // 기준 자료에 키 열이나 compare 필드 열이 없어도 오류다. 머리 행이 첫 줄이 아니면 그 줄이다.
    [Fact]
    public void Missing_column_in_reference_is_an_error()
    {
        CompareInput.AssertErrors(CompareInput.Run(Definition, "key,n,t\nk1,1,a\n", Good), "reference/a.csv:1: column id: 머리 행에 열이 없다");
        CompareInput.AssertErrors(CompareInput.Run(Definition, "\n\nid,t\nk1,a\n", Good), "reference/a.csv:3: column n: 머리 행에 열이 없다");
    }

    // 머리 행에 column 오류가 있으면 그 파일의 데이터 행은 보지 않는다(빈 키·숫자 오류가 나오지 않는다).
    [Fact]
    public void Data_rows_are_not_checked_when_header_has_column_error()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,n\n,x\nk1,y\nk1,z\n", Good),
            "reference/a.csv:1: column t: 머리 행에 열이 없다");
    }

    // 쓰는 열(키·compare)이 머리 행에 둘 이상이면 둘째 칸에 duplicate-column. 메시지에 처음 칸 번호.
    [Fact]
    public void Duplicate_used_column_is_an_error()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,n,t,n\nk1,1,a,2\n", Good),
            "reference/a.csv:1:8: duplicate-column n: 머리 행에 같은 열이 둘 이상 있다 (처음: 2번째 칸)");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, Good, "id,n,t, id \nk1,1,a,k2\n"),
            "candidate/a.csv:1:8: duplicate-column id: 머리 행에 같은 열이 둘 이상 있다 (처음: 1번째 칸)");
    }

    // 쓰지 않는 열의 중복은 오류가 아니다. compare에 없는 열은 값이 달라도 일치에 영향이 없다.
    [Fact]
    public void Unused_columns_are_ignored()
    {
        Assert.Equal(OneMatch, CompareInput.Counts(CompareInput.Run(Definition, "id,n,t,x,x\nk1,1,a,p,q\n", Good)));
        Assert.Equal(OneMatch, CompareInput.Counts(CompareInput.Run(Definition, "id,x,n,t\nk1,zz,1,a\n", "id,n,t,y\nk1,1,a,ww\n")));
        Assert.Equal(OneMatch, CompareInput.Counts(CompareInput.Run(Definition, "id,signal_type,n,t\nk1,normal,1,a\n", "id,signal_type,n,t\nk1,irregular,1,a\n")));
    }

    // 열 순서는 기준·결과가 달라도 된다.
    [Fact]
    public void Column_order_may_differ_between_sides()
    {
        Assert.Equal(OneMatch, CompareInput.Counts(CompareInput.Run(Definition, Good, "t,id,n\na,k1,1\n")));
    }

    // 머리 행 이름은 앞뒤 공백을 뗀다(row_id 와 row_id는 같다). 대소문자는 구분한다.
    [Fact]
    public void Header_names_are_trimmed_and_case_sensitive()
    {
        Assert.Equal(OneMatch, CompareInput.Counts(CompareInput.Run(Definition, " id , n ,\tt\nk1,1,a\n", Good)));
        CompareInput.AssertErrors(CompareInput.Run(Definition, "ID,n,t\nk1,1,a\n", Good), "reference/a.csv:1: column id: 머리 행에 열이 없다");
        CompareInput.AssertErrors(CompareInput.Run(Definition, "id,N,t\nk1,1,a\n", Good), "reference/a.csv:1: column n: 머리 행에 열이 없다");
    }
}
