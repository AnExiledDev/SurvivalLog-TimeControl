using TimeControl.Core;
using Xunit;

namespace TimeControl.Tests;

/// <summary>The order's finish time and the countdowns under the clock at home, run against the fake HUD pages.</summary>
public class ClockNotesPageTests
{
    private const string HomeWithClock =
        "var home = openHome(); home.clock = home.document.body.appendChild(new FakeElement('div', 'clock-group'));";

    private const string CountdownNote = "home.document.getElementById('tc-countdown')";
    private const string OrderNote = "home.document.getElementById('tc-order-finish')";

    [Fact]
    public void A_note_goes_inside_the_clock_group_so_it_moves_with_the_clock()
    {
        var ui = new FakeWebUi(HomeWithClock);

        Assert.Equal("ok", ui.Send(Countdowns.Script("Night in 3h")));
        Assert.True(ui.Is($"{CountdownNote}.parentNode === home.clock"));
        Assert.Equal("Night in 3h", ui.Text($"{CountdownNote}.textContent"));
    }

    [Fact]
    public void A_note_never_takes_a_click_from_what_is_under_it()
    {
        var ui = new FakeWebUi(HomeWithClock);

        ui.Send(OrderFinish.Script("Current order done at 14:30"));

        Assert.Equal("none", ui.Text($"inlineStyle({OrderNote}, 'pointer-events')"));
    }

    [Fact]
    public void Each_countdown_keeps_its_own_line()
    {
        var ui = new FakeWebUi(HomeWithClock);

        ui.Send(Countdowns.Script("Night in 3h\nWave in 45m"));

        Assert.Equal("pre-line", ui.Text($"inlineStyle({CountdownNote}, 'white-space')"));
        Assert.Equal("Night in 3h\nWave in 45m", ui.Text($"{CountdownNote}.textContent"));
    }

    [Fact]
    public void The_order_note_sits_above_the_countdowns_whichever_comes_first()
    {
        var ui = new FakeWebUi(HomeWithClock);

        ui.Send(Countdowns.Script("Night in 3h"));
        ui.Send(OrderFinish.Script("Current order done at 14:30"));

        Assert.Equal("tc-order-finish,tc-countdown", ui.Text("home.clock.children.map(function (c) { return c.id; }).join(',')"));
    }

    [Fact]
    public void Sending_again_updates_the_one_note_in_place()
    {
        var ui = new FakeWebUi(HomeWithClock);

        ui.Send(Countdowns.Script("Night in 3h"));

        Assert.Equal("ok", ui.Send(Countdowns.Script("Night in 2h")));
        Assert.Equal(1.0, ui.Number("home.clock.children.length"));
        Assert.Equal("Night in 2h", ui.Text($"{CountdownNote}.textContent"));
    }

    [Fact]
    public void Empty_text_takes_the_note_off()
    {
        var ui = new FakeWebUi(HomeWithClock);

        ui.Send(OrderFinish.Script("Current order done at 14:30"));

        Assert.Equal("ok", ui.Send(OrderFinish.Script("")));
        Assert.False(ui.Is(OrderNote));
    }

    [Fact]
    public void Chinese_and_quote_marks_come_through_exactly()
    {
        var ui = new FakeWebUi(HomeWithClock);
        var chinese = OrderFinish.Label(14 * 3600, 1800, isChinese: true);

        ui.Send(OrderFinish.Script(chinese));
        ui.Send(Countdowns.Script("it's \\ late"));

        Assert.Equal(chinese, ui.Text($"{OrderNote}.textContent"));
        Assert.Equal("it's \\ late", ui.Text($"{CountdownNote}.textContent"));
    }

    [Fact]
    public void A_home_page_with_no_clock_on_it_gets_no_note()
    {
        var ui = new FakeWebUi("var home = openHome();");

        Assert.Equal("no clock on the page", ui.Send(Countdowns.Script("Night in 3h")));
        Assert.False(ui.Is(CountdownNote));
    }

    [Fact]
    public void With_the_home_page_not_open_the_reply_says_so()
    {
        var ui = new FakeWebUi("var countdown = openCountdown();");

        Assert.Equal("no CoreUI1 frame", ui.Send(Countdowns.Script("Night in 3h")));
    }

    [Fact]
    public void A_note_left_on_the_countdown_page_is_taken_off_it()
    {
        var ui = new FakeWebUi(HomeWithClock + " var countdown = openCountdown(); countdown.document.body.appendChild(new FakeElement('div')).id = 'tc-countdown';");

        ui.Send(Countdowns.Script("Night in 3h"));

        Assert.False(ui.Is("countdown.document.getElementById('tc-countdown')"));
        Assert.True(ui.Is(CountdownNote));
    }
}
