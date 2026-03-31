---
name: visual-qa
description: Performs visual stress testing to identify layout overflows, responsiveness issues, and rendering artifacts.
---

# Visual QA & Stress Testing Protocol

This skill guides the agent to perform rigorous visual testing on the web application, focusing on "breaking" the layout to ensure robustness.

## 1. Viewport Stress Test (Responsiveness)
Use the browser agent to simulate different device sizes.
**Target Viewports:**
1.  **Mobile (Portrait)**: Width ~375px.
2.  **Tablet (Portrait)**: Width ~768px.
3.  **Desktop (Standard)**: Width ~1366px. Or maximize window.

**Validation Checklist:**
- [ ] **Horizontal Scroll**: Run `document.body.scrollWidth > window.innerWidth`. If true, content is overflowing horizontally (fail).
- [ ] **Menu Collapse**: Ensure navigation menus collapse into hamburgers or compact modes on Mobile/Tablet.
- [ ] **Modal Fit**: Ensure modals fit within the viewport height/width on small screens.

## 2. Content Overflow Test (The "Long Text" Test)
Simulate data overflow by injecting large content into text elements. This tests if the UI handles real-world dynamic data (like long German names) without breaking.

**Action (JS Injection):**
Run this in the browser console or via `execute_browser_javascript`:
```javascript
// Flood test: Inject long text into common text containers
(function() {
    const longText = "OVERFLOW_TEST_" + "Lorem ipsum dolor sit amet ".repeat(5);
    // Target common text elements
    const targets = document.querySelectorAll('h1, h2, h3, .card-title, .table-cell, p, span.value, .btn');
    targets.forEach(el => {
        el.setAttribute('data-original', el.innerText);
        el.innerText = longText;
        el.style.border = "1px solid red"; // Highlight testing elements
    });
    return "Injected long text into " + targets.length + " elements.";
})();
```

**Validation Checklist:**
- [ ] **Clipping**: Does text cut off unexpectedly?
- [ ] **Protrusion**: Does text extend outside its container (card/div)?
- [ ] **Alignment**: Do buttons or icons get pushed into unusable positions?

## 3. Boundary & Edge Case Analysis
- **Zero State**: Verify UI appearance when lists/tables are empty (e.g., "No items found" message).
- **Many items**: Verify UI appearance with 20+ items (Pagination vs Infinite Scroll).

## 4. Reporting
If issues are found, report them with:
- **Screenshot**: Capture the visual breakage.
- **Context**: "At 375px width, the 'Create Job' button enters the 'Job Name' input field."
- **Proposed Fix**: e.g., "Add `text-overflow: ellipsis; overflow: hidden; white-space: nowrap;` to the card title."
