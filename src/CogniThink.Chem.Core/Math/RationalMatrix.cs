namespace CogniThink.Chem.Core.Math;

/// <summary>
/// 有理数矩阵：高斯-若尔当消元（RREF）与零空间基求解。
/// 化学配平 = 求原子/电荷守恒齐次方程组 Ax=0 的正整数解，即零空间中符号一致的基本向量。
/// 全程有理数运算，无浮点误差。
/// 高中适用边界：方程式物种数一般 ≤ 10，矩阵规模很小，O(r·c·min(r,c)) 足够。
/// </summary>
public sealed class RationalMatrix
{
    private readonly Rational[,] _a;

    /// <summary>创建 rows×cols 零矩阵。</summary>
    public RationalMatrix(int rows, int cols)
    {
        if (rows <= 0 || cols <= 0)
        {
            throw new ArgumentException("矩阵维度必须为正");
        }

        Rows = rows;
        Cols = cols;
        _a = new Rational[rows, cols];
    }

    public int Rows { get; }

    public int Cols { get; }

    /// <summary>按行列索引读写元素（0 起）。</summary>
    public Rational this[int r, int c]
    {
        get => _a[r, c];
        set => _a[r, c] = value;
    }

    /// <summary>由行向量构造矩阵（每行一个守恒方程）。</summary>
    public static RationalMatrix FromRows(IReadOnlyList<Rational[]> rows)
    {
        if (rows.Count == 0)
        {
            throw new ArgumentException("至少需要一行");
        }

        var cols = rows[0].Length;
        var m = new RationalMatrix(rows.Count, cols);
        for (var r = 0; r < rows.Count; r++)
        {
            if (rows[r].Length != cols)
            {
                throw new ArgumentException("各行长度不一致");
            }

            for (var c = 0; c < cols; c++)
            {
                m[r, c] = rows[r][c];
            }
        }

        return m;
    }

    /// <summary>
    /// 高斯-若尔当消元，把矩阵化为行最简形（RREF）。
    /// 返回主元列索引（升序）。选主元策略：每列找第一个非零元（精确运算无需比较大小）。
    /// </summary>
    public List<int> ReducedRowEchelon()
    {
        var pivots = new List<int>();
        var pivotRow = 0;

        for (var col = 0; col < Cols && pivotRow < Rows; col++)
        {
            var found = -1;
            for (var r = pivotRow; r < Rows; r++)
            {
                if (!_a[r, col].IsZero)
                {
                    found = r;
                    break;
                }
            }

            if (found < 0)
            {
                continue;
            }

            SwapRows(pivotRow, found);

            // 主元归一
            var pivot = _a[pivotRow, col];
            for (var c = 0; c < Cols; c++)
            {
                _a[pivotRow, c] /= pivot;
            }

            // 消去其余行的该列
            for (var r = 0; r < Rows; r++)
            {
                if (r == pivotRow || _a[r, col].IsZero)
                {
                    continue;
                }

                var factor = _a[r, col];
                for (var c = 0; c < Cols; c++)
                {
                    _a[r, c] -= factor * _a[pivotRow, c];
                }
            }

            pivots.Add(col);
            pivotRow++;
        }

        return pivots;
    }

    /// <summary>
    /// 求齐次方程组 Ax=0 的零空间基。
    /// 每个基向量长度 = Cols：自由列位置取 1，主元行位置取 RREF 对应元素的相反数。
    /// 返回值个数 = Cols − 主元个数（零空间维数）。
    /// 易错点：调用后本矩阵已被化为 RREF（原地修改）。
    /// </summary>
    public List<Rational[]> NullSpaceBasis()
    {
        var pivots = ReducedRowEchelon();
        var pivotSet = new HashSet<int>(pivots);
        var basis = new List<Rational[]>();

        for (var free = 0; free < Cols; free++)
        {
            if (pivotSet.Contains(free))
            {
                continue;
            }

            var vec = new Rational[Cols];
            vec[free] = Rational.One;
            for (var i = 0; i < pivots.Count; i++)
            {
                vec[pivots[i]] = -_a[i, free];
            }

            basis.Add(vec);
        }

        return basis;
    }

    private void SwapRows(int r1, int r2)
    {
        if (r1 == r2)
        {
            return;
        }

        for (var c = 0; c < Cols; c++)
        {
            (_a[r1, c], _a[r2, c]) = (_a[r2, c], _a[r1, c]);
        }
    }
}
