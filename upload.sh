mv -f ../_output/linux/migration.zip ../_output/linux/linux.zip
mv -f ../_output/windows/migration.zip ../_output/windows/windows.zip
butler push ../_output/linux/linux.zip apia46/migration:linux
butler push ../_output/windows/windows.zip apia46/migration:windows
