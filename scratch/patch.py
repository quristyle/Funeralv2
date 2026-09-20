import re

with open('.github/workflows/deploy.yml', 'r', encoding='utf-8') as f:
    content = f.read()

# We want to add the `env:` block before `with:` in the `docker/build-push-action` step for web.
# And change `dxlicense=${{ secrets.DEVEXPRESS_LICENSE }}` to `dxlicense=env=DEVEXPRESS_LICENSE`

new_content = content.replace(
'''      - uses: docker/build-push-action@v6
        with:
          context: web''',
'''      - uses: docker/build-push-action@v6
        env:
          DEVEXPRESS_LICENSE: ${{ secrets.DEVEXPRESS_LICENSE }}
        with:
          context: web''')

new_content = new_content.replace(
'''          secrets: |
            dxlicense=${{ secrets.DEVEXPRESS_LICENSE }}''',
'''          secrets: |
            "dxlicense=env=DEVEXPRESS_LICENSE"''')

with open('.github/workflows/deploy.yml', 'w', encoding='utf-8') as f:
    f.write(new_content)
print("Patched successfully")
