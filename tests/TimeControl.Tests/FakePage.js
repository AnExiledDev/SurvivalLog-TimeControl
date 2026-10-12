// Just enough of Survival Log's web UI for Time Control's speed button scripts to run in Jint: the root page holding
// the HUD pages in iframes, and in each frame a document with elements, the few selectors the scripts use, the
// page's Vue state and its computed style. Test code only; nothing here ships.

var FakeElement = class {
    constructor(tagName, className) {
        var element = this;

        this.tagName = tagName;
        this.id = '';
        this.parentNode = null;
        this.children = [];
        this.textContent = '';
        this.attrs = {};
        this.classes = className ? className.split(' ') : [];
        this.style = { cssText: '' };
        this.onclick = null;
        this.markup = '';
        this.isHidden = false;
        this.right = 0;
        this.classList = {
            contains: function (name) { return element.classes.indexOf(name) >= 0; },
            toggle: function (name, force) {
                var isOn = force === undefined ? element.classes.indexOf(name) < 0 : !!force;

                element.classes = element.classes.filter(function (c) { return c !== name; });

                if (isOn) element.classes.push(name);

                return isOn;
            }
        };
    }

    get className() { return this.classes.join(' '); }

    set className(value) { this.classes = String(value).split(' ').filter(function (c) { return c.length > 0; }); }

    get innerHTML() { return this.markup; }

    // Only the markup the button script writes: an icon, then empty divs that each become a child with their class.
    set innerHTML(value) {
        var emptyDiv = /<div class="([^"]*)"><\/div>/g;
        var match;

        this.markup = String(value);
        this.children.forEach(function (child) { child.parentNode = null; });
        this.children = [];

        while ((match = emptyDiv.exec(this.markup)) !== null) this.appendChild(new FakeElement('div', match[1]));
    }

    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name) ? this.attrs[name] : null; }

    setAttribute(name, value) { this.attrs[name] = String(value); }

    // As in a browser, appending an element that already has a parent moves it.
    appendChild(child) {
        if (child.parentNode) child.parentNode.removeChild(child);

        child.parentNode = this;
        this.children.push(child);

        return child;
    }

    removeChild(child) {
        this.children = this.children.filter(function (c) { return c !== child; });
        child.parentNode = null;

        return child;
    }

    getBoundingClientRect() { return { right: this.right }; }

    click() { if (this.onclick) this.onclick.call(this, {}); }

    querySelectorAll(selector) {
        return descendantsOf(this).filter(function (element) { return matches(element, selector); });
    }

    querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
};

// One simple selector: a tag, a class or an id, optionally followed by :not() of another.
function matches(element, selector) {
    var not = /^(.+):not\((.+)\)$/.exec(selector);

    if (not) return matches(element, not[1]) && !matches(element, not[2]);
    if (selector.charAt(0) === '.') return element.classList.contains(selector.slice(1));
    if (selector.charAt(0) === '#') return element.id === selector.slice(1);
    if (/^[a-z]+$/.test(selector)) return element.tagName === selector;

    throw new Error('the fake page does not know the selector "' + selector + '"');
}

function descendantsOf(root) {
    var found = [];

    root.children.forEach(function walk(child) {
        found.push(child);
        child.children.forEach(walk);
    });

    return found;
}

function makeDocument() {
    var root = new FakeElement('html');

    return {
        documentElement: root,
        head: root.appendChild(new FakeElement('head')),
        body: root.appendChild(new FakeElement('body')),
        createElement: function (tagName) { return new FakeElement(tagName); },
        getElementById: function (id) { return descendantsOf(root).filter(function (element) { return element.id === id; })[0] || null; },
        querySelector: function (selector) { return root.querySelector(selector); },
        querySelectorAll: function (selector) { return root.querySelectorAll(selector); }
    };
}

// The root page the plugin runs its scripts in.
var document = makeDocument();

// A HUD page loaded in an iframe of the root page. state is the page's Vue state, or null for a page without one.
function openFrame(file, state) {
    var frame = document.body.appendChild(new FakeElement('iframe'));
    var page = {
        document: makeDocument(),
        location: 'https://webui.local/UI/' + file + '/' + file + '.html',
        eval: function (name) {
            if (name === 'state' && state) return state;

            throw new ReferenceError(name + ' is not defined');
        },
        getComputedStyle: function (element) { return { display: element.isHidden ? 'none' : 'block' }; }
    };

    frame.contentWindow = page;

    return page;
}

// CoreUI0 as the countdown before the disaster draws it: the bottom-left buttons, whose right edge sits at 230px and
// which hide in the planning view. It has no speed button texts. spec: { isPlanning }
function openCountdown(spec) {
    var page = openFrame('CoreUI0', null);

    page.bottomLeft = page.document.body.appendChild(new FakeElement('div', 'bottom-left'));
    page.bottomLeft.right = 230;
    page.bottomLeft.isHidden = !!(spec && spec.isPlanning);

    return page;
}

// CoreUI1 as an expedition draws it: the middle action group holding the order queue's wrapper, where the game's own
// speed buttons go when it shows them. spec: { state, hasOwnButtons, hasNoQueue }
function openHome(spec) {
    var settings = spec || {};
    var page = openFrame('CoreUI1', settings.state || null);

    page.middle = page.document.body.appendChild(new FakeElement('div', 'action-group-middle'));
    page.queue = settings.hasNoQueue ? null : page.middle.appendChild(new FakeElement('div', 'queue-flow-wrapper'));

    if (settings.hasOwnButtons) showOwnButtons(page);

    return page;
}

function showOwnButtons(page) {
    (page.queue || page.middle).appendChild(new FakeElement('div', 'speed-controls'));
}

// A page reload, or anything else that drops the mod's buttons from the page.
function loseButtons(page) {
    var group = buttonsOn(page);

    group.parentNode.removeChild(group);
}

function buttonsOn(page) { return page.document.getElementById('tc-speed-buttons'); }

function buttonsIn(page) { return buttonsOn(page).querySelectorAll('.speed-btn'); }

function buttonFor(page, level) {
    return buttonsIn(page).filter(function (button) { return button.getAttribute('data-level') === String(level); })[0];
}

function levelsOn(page) {
    return buttonsIn(page).map(function (button) { return button.getAttribute('data-level'); }).join(',');
}

function litOn(page) {
    return buttonsIn(page)
        .filter(function (button) { return button.classList.contains('active'); })
        .map(function (button) { return button.getAttribute('data-level'); })
        .join(',');
}

function tooltipFor(page, level) { return buttonFor(page, level).querySelector('.speed-tooltip').textContent; }
