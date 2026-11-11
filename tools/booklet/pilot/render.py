"""Print tools/booklet/pilot/booklet_pilot.html to a half-letter PDF with Playwright's Chromium."""
import asyncio, pathlib
from playwright.async_api import async_playwright
HERE = pathlib.Path(__file__).resolve().parent
async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch()
        pg = await b.new_page()
        await pg.goto((HERE / 'booklet_pilot.html').as_uri()); await pg.wait_for_timeout(800)
        await pg.pdf(path=str(HERE / 'out' / 'RTS_2027_Field_Manual_pilot.pdf'), width='5.5in', height='8.5in',
                     print_background=True, prefer_css_page_size=True)
        await b.close()
(HERE / 'out').mkdir(exist_ok=True)
asyncio.run(main())
