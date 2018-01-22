using System.Collections.Generic;
using System.Linq;

namespace Lib.Spriter
{
    public class Sprite2dPlacer
    {
        static public Dim2 Place(IList<ISpritePlace> sprites)
        {
            var a = new Sprite2dPlacer();
            foreach (var sprite in sprites.OrderByDescending(s => s.Height))
            {
                a.Add(sprite);
            }
            return a.Dim;
        }

        List<int> Widths = new List<int>();
        List<int> Heights = new List<int>();
        List<bool> Covs = new List<bool>();

        public Dim2 Dim
        {
            get
            {
                return new Dim2(Widths.Sum(), Heights.Sum());
            }
        }

        void AddCol(int v)
        {
            var oldwidth = Widths.Count;
            Widths.Add(v);
            var newcovs = new List<bool>(Widths.Count * Heights.Count);
            for (var iy = 0; iy < Heights.Count; iy++)
            {
                var rowstart = iy * oldwidth;
                for (var ix = 0; ix < oldwidth; ix++)
                {
                    newcovs.Add(Covs[rowstart + ix]);
                }
                newcovs.Add(false);
            }
            Covs = newcovs;
        }

        void AddRow(int v)
        {
            var oldheight = Heights.Count;
            Heights.Add(v);
            Covs.Capacity = Widths.Count * Heights.Count;
            for (var ix = 0; ix < Widths.Count; ix++)
            {
                Covs.Add(false);
            }
        }

        void SplitCol(int idxpos, int v)
        {
            var oldwidth = Widths.Count;
            Widths.Insert(idxpos, v);
            Widths[idxpos + 1] -= v;
            var newcovs = new List<bool>();
            for (var iy = 0; iy < Heights.Count; iy++)
            {
                var rowstart = iy * oldwidth;
                for (var ix = 0; ix < idxpos; ix++)
                {
                    newcovs.Add(Covs[rowstart + ix]);
                }
                newcovs.Add(Covs[rowstart + idxpos]);
                for (var ix = idxpos; ix < oldwidth; ix++)
                {
                    newcovs.Add(Covs[rowstart + ix]);
                }
            }
            Covs = newcovs;
        }

        void SplitRow(int idxpos, int v)
        {
            var width = Widths.Count;
            var oldheight = Heights.Count;
            Heights.Insert(idxpos, v);
            Heights[idxpos + 1] -= v;
            var newcovs = new List<bool>();
            for (var iy = 0; iy <= idxpos; iy++)
            {
                var rowstart = iy * width;
                for (var ix = 0; ix < width; ix++)
                {
                    newcovs.Add(Covs[rowstart + ix]);
                }
            }
            for (var iy = idxpos; iy < oldheight; iy++)
            {
                var rowstart = iy * width;
                for (var ix = 0; ix < width; ix++)
                {
                    newcovs.Add(Covs[rowstart + ix]);
                }
            }
            Covs = newcovs;
        }

        bool IsFree(int posx, int posy, int idxx, int idxy, int width, int height)
        {
            if (idxx >= Widths.Count)
                return true;
            if (idxy >= Heights.Count)
                return true;
            if (Covs[idxy * Widths.Count + idxx])
                return false;
            var w = 0;
            while (width > 0 && idxx + w < Widths.Count)
            {
                width -= Widths[idxx + w];
                w++;
            }
            var h = 0;
            while (height > 0 && idxy + h < Heights.Count)
            {
                height -= Heights[idxy + h];
                h++;
            }
            var covwidth = Widths.Count;
            var start = idxy * covwidth + idxx;
            for (var iy = 0; iy < h; iy++)
            {
                for (var ix = 0; ix < w; ix++)
                {
                    if (Covs[start + iy * covwidth + ix])
                        return false;
                }
            }
            return true;
        }

        void Fill(int posx, int posy, int idxx, int idxy, int width, int height)
        {
            var w = 0;
            while (width > 0 && idxx + w < Widths.Count)
            {
                width -= Widths[idxx + w];
                w++;
                if (width < 0)
                {
                    width += Widths[idxx + w - 1];
                    SplitCol(idxx + w - 1, width);
                    width = 0;
                    break;
                }
            }
            if (width > 0)
            {
                AddCol(width);
                w++;
            }
            var h = 0;
            while (height > 0 && idxy + h < Heights.Count)
            {
                height -= Heights[idxy + h];
                h++;
                if (height < 0)
                {
                    height += Heights[idxy + h - 1];
                    SplitRow(idxy + h - 1, height);
                    height = 0;
                    break;
                }
            }
            if (height > 0)
            {
                AddRow(height);
                h++;
            }
            var covwidth = Widths.Count;
            var start = idxy * covwidth + idxx;
            for (var iy = 0; iy < h; iy++)
            {
                for (var ix = 0; ix < w; ix++)
                {
                    Covs[start + iy * covwidth + ix] = true;
                }
            }
        }

        public void Add(ISpritePlace sprite)
        {
            var oldDim = Dim;
            var addpx = int.MaxValue;
            var bestx = 0;
            var besty = 0;
            var bestix = 0;
            var bestiy = 0;
            var aHeight = sprite.Height + 1;
            var aWidth = sprite.Width + 1;
            bool isImprovement(int x, int y)
            {
                if (x <= oldDim.Width)
                    x = oldDim.Width;
                if (y <= oldDim.Height)
                    y = oldDim.Height;
                var n = x * y - oldDim.Width * oldDim.Height;
                if (addpx != int.MaxValue)
                {
                    if (x > y * 2 && x - oldDim.Width > 0)
                        return false;
                    if (y > x * 2 && y - oldDim.Height > 0)
                        return false;
                }
                if (addpx > n)
                {
                    addpx = n;
                    return true;
                }
                return false;
            }
            if (oldDim.Width <= oldDim.Height)
            {
                if (isImprovement(oldDim.Width + aWidth, aHeight))
                {
                    bestx = oldDim.Width;
                    besty = 0;
                    bestix = Widths.Count;
                    bestiy = 0;
                }
            }
            else
            {
                if (isImprovement(aWidth, oldDim.Height + aHeight))
                {
                    besty = oldDim.Height;
                    bestx = 0;
                    bestix = 0;
                    bestiy = Heights.Count;
                }
            }
            var posy = 0;
            for (var iy = 0; iy < Heights.Count; iy++)
            {
                var posx = 0;
                for (var ix = 0; ix < Widths.Count; ix++)
                {
                    if (IsFree(posx, posy, ix, iy, aWidth, aHeight))
                    {
                        if (isImprovement(posx + aWidth, posy + aHeight))
                        {
                            bestx = posx;
                            besty = posy;
                            bestix = ix;
                            bestiy = iy;
                            if (addpx == 0)
                                break;
                        }
                    }
                    posx += Widths[ix];
                }
                if (addpx == 0)
                    break;
                posy += Heights[iy];
            }
            sprite.X = bestx;
            sprite.Y = besty;
            Fill(bestx, besty, bestix, bestiy, aWidth, aHeight);
        }
    }
}
