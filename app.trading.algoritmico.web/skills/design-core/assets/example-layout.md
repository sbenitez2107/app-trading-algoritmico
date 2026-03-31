# Reference Layout Implementation

> **Note**: This example uses Tailwind CSS for quick visualization. **For actual implementation, you MUST use the SCSS/BEM patterns defined in the `skill.md`** (e.g., `c-card`, `l-sidebar`) and the Design Tokens (CSS Variables). Do not use Tailwind classes in production code unless explicitly authorized.

This example demonstrates the correct nesting of the "Shell" (Sidebar, Header) and "Content" areas.

```html
<!DOCTYPE html>
<html class="dark" lang="en"><head>
<meta charset="utf-8"/>
<meta content="width=device-width, initial-scale=1.0" name="viewport"/>
<title>Catalog Readiness Analytics - PIM Pro</title>
<!-- DEMONSTRATION ONLY: Tailwind used for visual preview -->
<script src="https://cdn.tailwindcss.com?plugins=forms,container-queries"></script>
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:wght,FILL@100..700,0..1&amp;display=swap" rel="stylesheet"/>
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800;900&amp;display=swap" rel="stylesheet"/>
</head>
<body class="bg-background-light dark:bg-background-dark text-slate-900 dark:text-white font-display overflow-hidden h-screen flex">
<!-- Sidebar -->
<aside class="w-64 h-full bg-[#121317] border-r border-border-dark flex flex-col shrink-0 transition-all duration-300">
<div class="flex h-full flex-col justify-between p-4">
<div class="flex flex-col gap-6">
<!-- Branding -->
<div class="flex gap-3 items-center px-2">
<div class="bg-center bg-no-repeat aspect-square bg-cover rounded-full size-10 shadow-lg shadow-primary/20" data-alt="PIM Pro Logo abstract blue gradient" style='background-image: url("https://lh3.googleusercontent.com/aida-public/AB6AXuAHUVlgOe1ekjxkGp6vKGDbXGDjSL61WX7HNmw3_R4TU48UO8oldDmU7wogqhRxQcD3Qps35BhLXjE78uox-lW5i1Rb9Jq_uCkInYW6vXAR0-SNfawRrTTj_bkJxUZLW96k4PtZ4TAoY_CKNJUssgPfKiTEyiMtN-2bP-QoxFvSRI2d9xzQ1nZwlLKMad8HTFCHOtR9MB7ht2pWdpvgxYmVwz1imwoskCsEpqeztdmVz-IJ92L1wQVtCyUWPELqCa52hXurHf7Gtr4");'></div>
<div class="flex flex-col">
<h1 class="text-white text-base font-bold leading-normal tracking-wide">PIM Pro</h1>
<p class="text-[#a1a4b5] text-xs font-normal leading-normal">Enterprise Edition</p>
</div>
</div>
<!-- Navigation -->
<div class="flex flex-col gap-2">
<a class="flex items-center gap-3 px-3 py-2 rounded-lg text-[#a1a4b5] hover:bg-[#2b2c36] hover:text-white transition-colors group" href="#">
<span class="material-symbols-outlined text-[#a1a4b5] group-hover:text-white">dashboard</span>
<p class="text-sm font-medium leading-normal">Dashboard</p>
</a>
<a class="flex items-center gap-3 px-3 py-2 rounded-lg text-[#a1a4b5] hover:bg-[#2b2c36] hover:text-white transition-colors group" href="#">
<span class="material-symbols-outlined text-[#a1a4b5] group-hover:text-white">inventory_2</span>
<p class="text-sm font-medium leading-normal">Catalog</p>
</a>
<a class="flex items-center gap-3 px-3 py-2 rounded-lg text-[#a1a4b5] hover:bg-[#2b2c36] hover:text-white transition-colors group" href="#">
<span class="material-symbols-outlined text-[#a1a4b5] group-hover:text-white">image</span>
<p class="text-sm font-medium leading-normal">Assets</p>
</a>
<a class="flex items-center gap-3 px-3 py-2 rounded-lg bg-primary/10 border border-primary/20 text-white shadow-sm" href="#">
<span class="material-symbols-outlined text-primary icon-fill">analytics</span>
<p class="text-sm font-medium leading-normal">Analytics</p>
</a>
<a class="flex items-center gap-3 px-3 py-2 rounded-lg text-[#a1a4b5] hover:bg-[#2b2c36] hover:text-white transition-colors group" href="#">
<span class="material-symbols-outlined text-[#a1a4b5] group-hover:text-white">settings</span>
<p class="text-sm font-medium leading-normal">Settings</p>
</a>
</div>
</div>
<!-- Bottom Actions -->
<div class="px-2">
<button class="flex w-full items-center gap-3 px-3 py-2 rounded-lg text-[#a1a4b5] hover:bg-[#2b2c36] hover:text-white transition-colors">
<span class="material-symbols-outlined">logout</span>
<p class="text-sm font-medium">Log Out</p>
</button>
</div>
</div>
</aside>
<!-- Main Content -->
<main class="flex-1 flex flex-col h-full overflow-y-auto relative">
<div class="layout-content-container flex flex-col max-w-[1200px] w-full mx-auto p-6 gap-6">
<!-- Breadcrumbs -->
<div class="flex flex-wrap gap-2 items-center">
<a class="text-[#a1a4b5] text-sm font-medium hover:text-white transition-colors" href="#">Home</a>
<span class="text-[#a1a4b5] text-sm">/</span>
<a class="text-[#a1a4b5] text-sm font-medium hover:text-white transition-colors" href="#">Analytics</a>
<span class="text-[#a1a4b5] text-sm">/</span>
<span class="text-white text-sm font-medium">Catalog Readiness</span>
</div>
<!-- Page Heading & Actions -->
<div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
<div>
<h1 class="text-white text-3xl font-black leading-tight tracking-tight">Catalog Readiness Analytics</h1>
<p class="text-[#a1a4b5] text-sm mt-1">Real-time insights into your product data completeness.</p>
</div>
<div class="flex gap-3">
<button class="flex items-center justify-center gap-2 rounded-lg h-10 px-4 border border-border-dark bg-surface-dark text-white text-sm font-bold hover:bg-[#2b2c36] transition-colors">
<span class="material-symbols-outlined text-[20px]">share</span>
<span>Share</span>
</button>
<button class="flex items-center justify-center gap-2 rounded-lg h-10 px-4 bg-primary hover:bg-primary/90 text-white text-sm font-bold shadow-lg shadow-primary/25 transition-all">
<span class="material-symbols-outlined text-[20px]">download</span>
<span>Export Report</span>
</button>
</div>
</div>
<!-- Filters (Chips) -->
<div class="flex gap-3 flex-wrap">
<button class="flex h-9 items-center justify-center gap-x-2 rounded-lg bg-surface-dark border border-border-dark pl-4 pr-3 hover:border-primary/50 transition-colors">
<p class="text-white text-sm font-medium">Date: Last 30 Days</p>
<span class="material-symbols-outlined text-[#a1a4b5] text-[20px]">expand_more</span>
</button>
<button class="flex h-9 items-center justify-center gap-x-2 rounded-lg bg-surface-dark border border-border-dark pl-4 pr-3 hover:border-primary/50 transition-colors">
<p class="text-white text-sm font-medium">Category: Electronics</p>
<span class="material-symbols-outlined text-[#a1a4b5] text-[20px]">expand_more</span>
</button>
<button class="flex h-9 items-center justify-center gap-x-2 rounded-lg bg-surface-dark border border-border-dark pl-4 pr-3 hover:border-primary/50 transition-colors">
<p class="text-white text-sm font-medium">Channel: All</p>
<span class="material-symbols-outlined text-[#a1a4b5] text-[20px]">expand_more</span>
</button>
<button class="flex h-9 items-center justify-center gap-x-2 rounded-lg bg-transparent border border-dashed border-[#a1a4b5] pl-3 pr-3 hover:border-white hover:text-white text-[#a1a4b5] transition-colors">
<span class="material-symbols-outlined text-[20px]">add</span>
<p class="text-sm font-medium">Add Filter</p>
</button>
</div>
<!-- KPI Stats Row -->
<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
<!-- Stat 1 -->
<div class="flex flex-col gap-1 rounded-xl p-5 border border-border-dark bg-surface-dark shadow-sm">
<div class="flex justify-between items-start">
<p class="text-[#a1a4b5] text-sm font-medium uppercase tracking-wider">Total Products</p>
<span class="material-symbols-outlined text-[#a1a4b5] text-[20px]">inventory_2</span>
</div>
<div class="flex items-baseline gap-3 mt-2">
<p class="text-white text-3xl font-bold font-mono tracking-tight">12,405</p>
<div class="flex items-center gap-1 text-[#0bda65] bg-[#0bda65]/10 px-2 py-0.5 rounded text-xs font-semibold">
<span class="material-symbols-outlined text-[14px]">trending_up</span>
<span>+12%</span>
</div>
</div>
<p class="text-[#a1a4b5] text-xs mt-1">vs. last month</p>
</div>
<!-- Stat 2 -->
<div class="flex flex-col gap-1 rounded-xl p-5 border border-border-dark bg-surface-dark shadow-sm">
<div class="flex justify-between items-start">
<p class="text-[#a1a4b5] text-sm font-medium uppercase tracking-wider">Enriched SKUs</p>
<span class="material-symbols-outlined text-[#a1a4b5] text-[20px]">check_circle</span>
</div>
<div class="flex items-baseline gap-3 mt-2">
<p class="text-white text-3xl font-bold font-mono tracking-tight">8,200</p>
<p class="text-[#a1a4b5] text-lg font-medium">(66%)</p>
<div class="flex items-center gap-1 text-[#0bda65] bg-[#0bda65]/10 px-2 py-0.5 rounded text-xs font-semibold">
<span class="material-symbols-outlined text-[14px]">trending_up</span>
<span>+5%</span>
</div>
</div>
<p class="text-[#a1a4b5] text-xs mt-1">Ready for publishing</p>
</div>
<!-- Stat 3 -->
<div class="flex flex-col gap-1 rounded-xl p-5 border border-border-dark bg-surface-dark shadow-sm">
<div class="flex justify-between items-start">
<p class="text-[#a1a4b5] text-sm font-medium uppercase tracking-wider">Avg Time to Market</p>
<span class="material-symbols-outlined text-[#a1a4b5] text-[20px]">timer</span>
</div>
<div class="flex items-baseline gap-3 mt-2">
<p class="text-white text-3xl font-bold font-mono tracking-tight">4.2 days</p>
<div class="flex items-center gap-1 text-[#0bda65] bg-[#0bda65]/10 px-2 py-0.5 rounded text-xs font-semibold">
<span class="material-symbols-outlined text-[14px]">arrow_downward</span>
<span>-1 day</span>
</div>
</div>
<p class="text-[#a1a4b5] text-xs mt-1">Efficiency improved</p>
</div>
</div>
<!-- Charts Section -->
<div class="grid grid-cols-1 lg:grid-cols-3 gap-4 min-h-[300px]">
<!-- Chart 1: Catalog Readiness (Donut) -->
<div class="lg:col-span-1 rounded-xl border border-border-dark bg-surface-dark p-6 flex flex-col justify-between">
<div>
<h3 class="text-white text-lg font-bold mb-1">Catalog Readiness</h3>
<p class="text-[#a1a4b5] text-sm">Overall data completeness across all SKUs.</p>
</div>
<div class="relative flex items-center justify-center my-6">
<!-- Conic Gradient for Donut Chart -->
<!-- 78% Complete (Primary), 15% Warning (Yellow), 7% Error (Red) -->
<div class="w-48 h-48 rounded-full" style="background: conic-gradient(#3248c3 0% 78%, #eab308 78% 93%, #ef4444 93% 100%);"></div>
<!-- Inner Circle for 'Donut' effect -->
<div class="absolute w-36 h-36 bg-surface-dark rounded-full flex flex-col items-center justify-center">
<span class="text-4xl font-black text-white">78%</span>
<span class="text-xs text-[#a1a4b5] uppercase tracking-wide font-medium">Ready</span>
</div>
</div>
<div class="flex justify-between px-2">
<div class="flex flex-col items-center">
<div class="flex items-center gap-2 mb-1">
<div class="w-3 h-3 rounded-full bg-primary"></div>
<span class="text-white text-sm font-bold">Good</span>
</div>
<span class="text-[#a1a4b5] text-xs">78%</span>
</div>
<div class="flex flex-col items-center">
<div class="flex items-center gap-2 mb-1">
<div class="w-3 h-3 rounded-full bg-yellow-500"></div>
<span class="text-white text-sm font-bold">Fair</span>
</div>
<span class="text-[#a1a4b5] text-xs">15%</span>
</div>
<div class="flex flex-col items-center">
<div class="flex items-center gap-2 mb-1">
<div class="w-3 h-3 rounded-full bg-red-500"></div>
<span class="text-white text-sm font-bold">Poor</span>
</div>
<span class="text-[#a1a4b5] text-xs">7%</span>
</div>
</div>
</div>
<!-- Chart 2: Channel Readiness (Bars) & Trend -->
<div class="lg:col-span-2 rounded-xl border border-border-dark bg-surface-dark p-6 flex flex-col">
<div class="flex justify-between items-center mb-6">
<div>
<h3 class="text-white text-lg font-bold mb-1">Channel Compliance</h3>
<p class="text-[#a1a4b5] text-sm">Readiness by sales channel requirements.</p>
</div>
<button class="text-primary text-sm font-medium hover:text-white transition-colors">View All Channels</button>
</div>
<div class="flex flex-col gap-6 flex-1 justify-center">
<!-- Amazon -->
<div class="flex flex-col gap-2">
<div class="flex justify-between items-end">
<div class="flex items-center gap-2">
<span class="material-symbols-outlined text-white">shopping_cart</span>
<span class="text-white font-medium">Amazon</span>
</div>
<span class="text-white font-bold">92%</span>
</div>
<div class="w-full bg-[#2b2c36] rounded-full h-2.5 overflow-hidden">
<div class="bg-[#0bda65] h-2.5 rounded-full" style="width: 92%"></div>
</div>
<p class="text-xs text-[#a1a4b5]">Missing: 8 critical attributes across 45 SKUs</p>
</div>
<!-- Shopify -->
<div class="flex flex-col gap-2">
<div class="flex justify-between items-end">
<div class="flex items-center gap-2">
<span class="material-symbols-outlined text-white">storefront</span>
<span class="text-white font-medium">Shopify</span>
</div>
<span class="text-white font-bold">65%</span>
</div>
<div class="w-full bg-[#2b2c36] rounded-full h-2.5 overflow-hidden">
<div class="bg-yellow-500 h-2.5 rounded-full" style="width: 65%"></div>
</div>
<p class="text-xs text-[#a1a4b5]">Missing: Product Descriptions, Meta Tags</p>
</div>
<!-- Google Shopping -->
<div class="flex flex-col gap-2">
<div class="flex justify-between items-end">
<div class="flex items-center gap-2">
<span class="material-symbols-outlined text-white">search</span>
<span class="text-white font-medium">Google Shopping</span>
</div>
<span class="text-white font-bold">88%</span>
</div>
<div class="w-full bg-[#2b2c36] rounded-full h-2.5 overflow-hidden">
<div class="bg-primary h-2.5 rounded-full" style="width: 88%"></div>
</div>
<p class="text-xs text-[#a1a4b5]">Missing: GTIN/MPN codes</p>
</div>
</div>
<!-- Trend Mini-Section -->
<div class="mt-6 pt-6 border-t border-border-dark flex items-center justify-between">
<div>
<p class="text-sm font-medium text-white">30-Day Trend</p>
<p class="text-xs text-[#a1a4b5]">Completeness score improvement</p>
</div>
<div class="flex gap-1 items-end h-10 w-32">
<div class="w-2 bg-primary/30 h-[40%] rounded-t-sm"></div>
<div class="w-2 bg-primary/30 h-[50%] rounded-t-sm"></div>
<div class="w-2 bg-primary/40 h-[45%] rounded-t-sm"></div>
<div class="w-2 bg-primary/40 h-[60%] rounded-t-sm"></div>
<div class="w-2 bg-primary/60 h-[55%] rounded-t-sm"></div>
<div class="w-2 bg-primary/70 h-[70%] rounded-t-sm"></div>
<div class="w-2 bg-primary/80 h-[85%] rounded-t-sm"></div>
<div class="w-2 bg-primary h-[90%] rounded-t-sm"></div>
</div>
</div>
</div>
</div>
<!-- Task List Section -->
<div class="flex flex-col gap-4">
<div class="flex justify-between items-end">
<h3 class="text-white text-xl font-bold flex items-center gap-2">
<span class="material-symbols-outlined text-yellow-500">warning</span>
                        Action Required: Top Missing Attributes
                    </h3>
<button class="text-[#a1a4b5] text-sm hover:text-white transition-colors flex items-center gap-1">
                        View Full Report <span class="material-symbols-outlined text-[16px]">arrow_forward</span>
</button>
</div>
<div class="w-full overflow-hidden rounded-xl border border-border-dark bg-surface-dark">
<div class="overflow-x-auto">
<table class="w-full text-left border-collapse">
<thead>
<tr class="border-b border-border-dark bg-[#2b2c36]">
<th class="p-4 text-xs font-medium text-[#a1a4b5] uppercase tracking-wider">Attribute Issue</th>
<th class="p-4 text-xs font-medium text-[#a1a4b5] uppercase tracking-wider">Impact Level</th>
<th class="p-4 text-xs font-medium text-[#a1a4b5] uppercase tracking-wider">Affected SKUs</th>
<th class="p-4 text-xs font-medium text-[#a1a4b5] uppercase tracking-wider">Example Product</th>
<th class="p-4 text-xs font-medium text-[#a1a4b5] uppercase tracking-wider text-right">Action</th>
</tr>
</thead>
<tbody class="divide-y divide-border-dark">
<!-- Row 1 -->
<tr class="hover:bg-white/5 transition-colors group">
<td class="p-4">
<div class="flex items-center gap-3">
<div class="p-2 bg-red-500/10 rounded-lg text-red-500">
<span class="material-symbols-outlined">attach_money</span>
</div>
<div>
<p class="text-white text-sm font-medium">Price Missing</p>
<p class="text-[#a1a4b5] text-xs">Core attribute required for all channels</p>
</div>
</div>
</td>
<td class="p-4">
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-500/10 text-red-500 border border-red-500/20">
<span class="w-1.5 h-1.5 rounded-full bg-red-500"></span>
                                            Critical
                                        </span>
</td>
<td class="p-4">
<p class="text-white font-mono text-sm">12 SKUs</p>
</td>
<td class="p-4">
<div class="flex items-center gap-2">
<div class="w-8 h-8 rounded bg-gray-700 bg-cover bg-center" data-alt="Small thumbnail of a black headphone" style="background-image: url('https://lh3.googleusercontent.com/aida-public/AB6AXuAv-1mGx4hTUKpHYixfpRmmtk35pU6mvNCbJ_cYwyZZMr6WRqaJYngVWai_vzrIegdL6fkBjSXwIgVT1MDhyHbHrthFXmynR5aZCRdzyFsF-O_HOjflANfbv9HGeed5cG-I9-wmrmqntziOjGWPSs_Xc1QV5hqMbdt_Pg8xm3NUCBqdDTBaxakW5ZrkoIAkq_CXqim_DCCMPTWyDnxufPzpWqKJcOjNeXUaaN-dN_U5YF3TFvPBFrBs6U-bD-fFQ6Logmkor_U2N1k')"></div>
<span class="text-[#a1a4b5] text-sm truncate max-w-[150px]">Wireless Headphones X1...</span>
</div>
</td>
<td class="p-4 text-right">
<button class="text-white bg-primary hover:bg-primary/90 text-xs font-bold py-2 px-4 rounded-lg transition-colors">
                                            Fix Now
                                        </button>
</td>
</tr>
<!-- Row 2 -->
<tr class="hover:bg-white/5 transition-colors group">
<td class="p-4">
<div class="flex items-center gap-3">
<div class="p-2 bg-yellow-500/10 rounded-lg text-yellow-500">
<span class="material-symbols-outlined">image_not_supported</span>
</div>
<div>
<p class="text-white text-sm font-medium">HD Images Missing</p>
<p class="text-[#a1a4b5] text-xs">Resolution below 1000x1000px</p>
</div>
</div>
</td>
<td class="p-4">
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-yellow-500/10 text-yellow-500 border border-yellow-500/20">
<span class="w-1.5 h-1.5 rounded-full bg-yellow-500"></span>
                                            High
                                        </span>
</td>
<td class="p-4">
<p class="text-white font-mono text-sm">120 SKUs</p>
</td>
<td class="p-4">
<div class="flex items-center gap-2">
<div class="w-8 h-8 rounded bg-gray-700 bg-cover bg-center" data-alt="Small thumbnail of a smartwatch" style="background-image: url('https://lh3.googleusercontent.com/aida-public/AB6AXuAUqn0_jfAFK7kW-zos_B3HuvGmKtMsGq_qqYw58ttHhQGi-elEcSnkbRgykHsUNPiTPjzOStBsARe96bVsJ9ED1ko3LWziO1lk-oPMRdqJzKXkRdjk7fpJ4fAf8gfoAqtqLmi8CCs7BK3rQO2oMrW6-BTYl5CzoTnCrFKOcolRCRAr7B2MnCPTi7OXnf2542nNAuwmw6uRoZehGSCnNZhnmryJ2aDOxWjofN0mEDzXLIhwvDDzPd3rqRGsNe4HcjrWLE9tqAqbMD0')"></div>
<span class="text-[#a1a4b5] text-sm truncate max-w-[150px]">Smart Watch Series 5...</span>
</div>
</td>
<td class="p-4 text-right">
<button class="text-white bg-surface-dark border border-border-dark hover:bg-[#2b2c36] text-xs font-bold py-2 px-4 rounded-lg transition-colors">
                                            Upload
                                        </button>
</td>
</tr>
<!-- Row 3 -->
<tr class="hover:bg-white/5 transition-colors group">
<td class="p-4">
<div class="flex items-center gap-3">
<div class="p-2 bg-blue-500/10 rounded-lg text-blue-400">
<span class="material-symbols-outlined">description</span>
</div>
<div>
<p class="text-white text-sm font-medium">Tech Specs Incomplete</p>
<p class="text-[#a1a4b5] text-xs">Missing dimensions or weight</p>
</div>
</div>
</td>
<td class="p-4">
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-blue-500/10 text-blue-400 border border-blue-500/20">
<span class="w-1.5 h-1.5 rounded-full bg-blue-400"></span>
                                            Medium
                                        </span>
</td>
<td class="p-4">
<p class="text-white font-mono text-sm">55 SKUs</p>
</td>
<td class="p-4">
<div class="flex items-center gap-2">
<div class="w-8 h-8 rounded bg-gray-700 bg-cover bg-center" data-alt="Small thumbnail of a camera lens" style="background-image: url('https://lh3.googleusercontent.com/aida-public/AB6AXuBczS2qyAwhQ12XxvN4dIEN6vIBQ2XEFmKBrxmoEwgxEOxaWm8So3D8YZoQ-D8AJgpCuWI7z4rpQxXTmWkzvzOTq881I6BznWcD1TrYWj5LzWkudkVo6wDi2PB_TC5bYksEB6JZyqYRhqAYlrYCmhAseGXPw-R-f8eNL9apDUzBdTXUslmAsGYra4jfjoGjvaIbv9NGJ6K-MpFAZsDj53rCOBn8qOXXHmVNecOqtMzY5Sm4p7UWuLEd0L5oLMrfVSJn6ViNZvxwfPI')"></div>
<span class="text-[#a1a4b5] text-sm truncate max-w-[150px]">Pro Camera Lens 50mm...</span>
</div>
</td>
<td class="p-4 text-right">
<button class="text-white bg-surface-dark border border-border-dark hover:bg-[#2b2c36] text-xs font-bold py-2 px-4 rounded-lg transition-colors">
                                            Edit Specs
                                        </button>
</td>
</tr>
</tbody>
</table>
</div>
<div class="px-4 py-3 border-t border-border-dark bg-[#2b2c36]/50 flex justify-center">
<button class="text-primary text-xs font-bold hover:underline">Show 15 more issues</button>
</div>
</div>
</div>
<!-- Footer Spacing -->
<div class="h-10"></div>
</div>
</main>
</body></html>
```
