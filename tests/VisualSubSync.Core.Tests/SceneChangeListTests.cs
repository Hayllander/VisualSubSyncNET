using VisualSubSync.Core.Scenes;

namespace VisualSubSync.Core.Tests;

public class SceneChangeListTests
{
    private static readonly int[] Scenes = [1000, 2000, 3000];

    [Fact]
    public void Binary_search_returns_the_next_or_the_previous_index()
    {
        Assert.Equal(1, SceneChangeList.BinarySearch(Scenes, 2000));
        Assert.Equal(1, SceneChangeList.BinarySearch(Scenes, 1500));
        Assert.Equal(0, SceneChangeList.BinarySearch(Scenes, 1500, backward: true));
        Assert.Equal(-1, SceneChangeList.BinarySearch(Scenes, 500, backward: true));
    }

    [Fact]
    public void Contains_checks_the_closed_interval()
    {
        Assert.True(SceneChangeList.Contains(Scenes, 2000, 2000));
        Assert.True(SceneChangeList.Contains(Scenes, 1500, 2000));
        Assert.False(SceneChangeList.Contains(Scenes, 2001, 2999));
        Assert.False(SceneChangeList.Contains(Scenes, 3001, 4000));
    }

    [Fact]
    public void Delete_removes_and_returns_the_scene_changes_in_range()
    {
        var list = Scenes.ToList();
        Assert.Equal([2000, 3000], SceneChangeList.Delete(list, 1500, 3500));
        Assert.Equal([1000], list);
        Assert.Empty(SceneChangeList.Delete(list, 100, 500));
        Assert.Empty(SceneChangeList.Delete(list, 1100, 1200));
        Assert.Equal([1000], list);
    }

    [Fact]
    public void Insert_merges_in_order_without_duplicates()
    {
        var list = Scenes.ToList();
        SceneChangeList.Insert(list, [500, 2000, 2500, 4000]);
        Assert.Equal([500, 1000, 2000, 2500, 3000, 4000], list);
    }
}
