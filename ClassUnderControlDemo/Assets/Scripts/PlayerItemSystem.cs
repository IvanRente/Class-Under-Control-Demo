using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerItemSystem : MonoBehaviour
{
    public enum ItemId
    {
        Shield,
        WaterPistol,
        AirHorn
    }

    [Serializable]
    public class ItemDefinition
    {
        public ItemId itemId;
        public string displayName;
        public int price = 25;
        public GameObject equipPrefab;
        public Transform equipAnchorOverride;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public Color uiColor = new Color(0.9f, 0.85f, 0.3f, 1f);
    }

    [Serializable]
    public class ItemButtonBinding
    {
        public ItemId itemId;
        public Button button;
        public TMP_Text label;
        public Image background;

        public void ResolveReferences()
        {
            if (button == null)
                return;

            if (background == null)
                background = button.targetGraphic as Image ?? button.GetComponent<Image>();

            if (label == null)
                label = button.GetComponentInChildren<TMP_Text>(true);
        }
    }

    static readonly ItemId[] InventoryOrder =
    {
        ItemId.Shield,
        ItemId.WaterPistol,
        ItemId.AirHorn
    };

    [Header("Input")]
    public KeyCode inventoryKey = KeyCode.I;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Gameplay")]
    public int startingMoney = 100;
    public Transform defaultEquipAnchor;
    public ItemDefinition[] itemDefinitions = new ItemDefinition[3];

    [Header("Inventory UI")]
    public GameObject inventoryPanel;
    public TMP_Text[] moneyLabels;
    public ItemButtonBinding[] inventorySlots = new ItemButtonBinding[3];

    [Header("Shop UI")]
    public GameObject shopPanel;
    public TMP_Text shopTitleLabel;
    public ItemButtonBinding[] shopSlots = new ItemButtonBinding[3];

    [Header("Debug")]
    public bool debugShopFlow = true;

    readonly Dictionary<ItemId, ItemDefinition> itemLookup = new Dictionary<ItemId, ItemDefinition>();
    readonly HashSet<ItemId> ownedItems = new HashSet<ItemId>();
    readonly Dictionary<ItemId, ItemButtonBinding> inventorySlotLookup = new Dictionary<ItemId, ItemButtonBinding>();
    readonly Dictionary<ItemId, ItemButtonBinding> shopSlotLookup = new Dictionary<ItemId, ItemButtonBinding>();

    int currentMoney;
    ItemId? equippedItemId;
    GameObject equippedVisual;
    VendorShop activeVendor;
    bool inventoryOpen;
    bool uiCallbacksBound;
    int shopOpenedFrame = -1;

    public int CurrentMoney => currentMoney;
    public bool IsAnyMenuOpen => inventoryOpen || activeVendor != null;
    public bool IsInventoryOpen => inventoryOpen;
    public bool IsShopOpen => activeVendor != null;
    public bool HasItem(ItemId itemId) => ownedItems.Contains(itemId);

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        RefreshUi();
    }

    void Reset()
    {
        EnsureDefinitionDefaults();
        EnsureUiBindingDefaults();
        ResolveUiReferences();
    }

    void OnValidate()
    {
        EnsureDefinitionDefaults();
        EnsureUiBindingDefaults();
        ResolveUiReferences();
    }

    void Awake()
    {
        currentMoney = Mathf.Max(0, startingMoney);
        RebuildLookup();
        ResolveUiReferences();
    }

    void Start()
    {
        BindUiCallbacks();
        RefreshUi();
    }

    void Update()
    {
        if (Input.GetKeyDown(inventoryKey))
            ToggleInventory();

        if (activeVendor != null && Input.GetKeyDown(interactKey) && Time.frameCount != shopOpenedFrame)
            CloseShop();

        if (Input.GetKeyDown(cancelKey) && IsAnyMenuOpen)
            CloseAllMenus();
    }

    public void ToggleInventory()
    {
        if (inventoryOpen)
        {
            CloseInventory();
            return;
        }

        CloseShop();
        inventoryOpen = true;
        RefreshUi();
    }

    public void ToggleShop(VendorShop vendor)
    {
        if (vendor == null)
        {
            if (debugShopFlow)
                Debug.Log("[PlayerItemSystem] ToggleShop called with null vendor.");

            return;
        }

        if (debugShopFlow)
        {
            Debug.Log("[PlayerItemSystem] ToggleShop called for vendor '" + vendor.DisplayName
                + "'. Shop panel assigned: " + (shopPanel != null)
                + ". Inventory open: " + inventoryOpen
                + ". Current active vendor: " + (activeVendor != null ? activeVendor.DisplayName : "none") + ".");
        }

        if (activeVendor == vendor)
        {
            CloseShop();
            return;
        }

        inventoryOpen = false;
        activeVendor = vendor;
        shopOpenedFrame = Time.frameCount;
        RefreshUi();

        if (debugShopFlow)
        {
            Debug.Log("[PlayerItemSystem] Shop toggled open. Active vendor: "
                + (activeVendor != null ? activeVendor.DisplayName : "none")
                + ". Shop panel active: " + (shopPanel != null && shopPanel.activeSelf) + ".");
        }
    }

    public void CloseAllMenus()
    {
        inventoryOpen = false;
        activeVendor = null;
        RefreshUi();
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        currentMoney += amount;
        RefreshUi();
    }

    public bool TryBuyItem(ItemId itemId)
    {
        if (ownedItems.Contains(itemId))
            return false;

        ItemDefinition definition;
        if (!itemLookup.TryGetValue(itemId, out definition))
            return false;

        if (currentMoney < definition.price)
            return false;

        currentMoney -= definition.price;
        ownedItems.Add(itemId);
        RefreshUi();
        return true;
    }

    public void ToggleEquip(ItemId itemId)
    {
        if (!ownedItems.Contains(itemId))
            return;

        if (equippedItemId.HasValue && equippedItemId.Value == itemId)
            UnequipCurrentItem();
        else
            EquipItem(itemId);

        RefreshUi();
    }

    void EquipItem(ItemId itemId)
    {
        UnequipCurrentItem();
        equippedItemId = itemId;

        ItemDefinition definition;
        if (!itemLookup.TryGetValue(itemId, out definition))
            return;

        Transform anchor = definition.equipAnchorOverride != null ? definition.equipAnchorOverride : defaultEquipAnchor;
        if (anchor == null || definition.equipPrefab == null)
            return;

        equippedVisual = Instantiate(definition.equipPrefab, anchor);
        equippedVisual.name = definition.equipPrefab.name + "_Equipped";
        equippedVisual.transform.localPosition = definition.localPosition;
        equippedVisual.transform.localRotation = Quaternion.Euler(definition.localEulerAngles);
        equippedVisual.transform.localScale = definition.localScale;

        DisablePhysicsOnEquippedItem(equippedVisual);
    }

    void UnequipCurrentItem()
    {
        equippedItemId = null;

        if (equippedVisual != null)
            Destroy(equippedVisual);

        equippedVisual = null;
    }

    void DisablePhysicsOnEquippedItem(GameObject itemRoot)
    {
        Collider[] colliders = itemRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] rigidbodies = itemRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }
    }

    void CloseInventory()
    {
        inventoryOpen = false;
        RefreshUi();
    }

    void CloseShop()
    {
        if (debugShopFlow)
        {
            Debug.Log("[PlayerItemSystem] Closing shop. Previous active vendor: "
                + (activeVendor != null ? activeVendor.DisplayName : "none") + ".");
        }

        activeVendor = null;
        RefreshUi();
    }

    void RebuildLookup()
    {
        itemLookup.Clear();

        if (itemDefinitions == null)
            return;

        for (int i = 0; i < itemDefinitions.Length; i++)
        {
            ItemDefinition definition = itemDefinitions[i];
            if (definition == null)
                continue;

            itemLookup[definition.itemId] = definition;
        }
    }

    void EnsureDefinitionDefaults()
    {
        if (itemDefinitions == null || itemDefinitions.Length != InventoryOrder.Length)
        {
            ItemDefinition[] resized = new ItemDefinition[InventoryOrder.Length];
            if (itemDefinitions != null)
            {
                for (int i = 0; i < itemDefinitions.Length && i < resized.Length; i++)
                    resized[i] = itemDefinitions[i];
            }

            itemDefinitions = resized;
        }

        for (int i = 0; i < InventoryOrder.Length; i++)
        {
            if (itemDefinitions[i] == null)
                itemDefinitions[i] = new ItemDefinition();

            itemDefinitions[i].itemId = InventoryOrder[i];

            if (string.IsNullOrWhiteSpace(itemDefinitions[i].displayName))
                itemDefinitions[i].displayName = GetDefaultDisplayName(InventoryOrder[i]);

            if (itemDefinitions[i].localScale == Vector3.zero)
                itemDefinitions[i].localScale = Vector3.one;
        }

        RebuildLookup();
    }

    void EnsureUiBindingDefaults()
    {
        inventorySlots = EnsureBindingArray(inventorySlots);
        shopSlots = EnsureBindingArray(shopSlots);

        if (moneyLabels == null)
            moneyLabels = new TMP_Text[0];
    }

    ItemButtonBinding[] EnsureBindingArray(ItemButtonBinding[] source)
    {
        if (source == null || source.Length != InventoryOrder.Length)
        {
            ItemButtonBinding[] resized = new ItemButtonBinding[InventoryOrder.Length];
            if (source != null)
            {
                for (int i = 0; i < source.Length && i < resized.Length; i++)
                    resized[i] = source[i];
            }

            source = resized;
        }

        for (int i = 0; i < InventoryOrder.Length; i++)
        {
            if (source[i] == null)
                source[i] = new ItemButtonBinding();

            source[i].itemId = InventoryOrder[i];
        }

        return source;
    }

    void ResolveUiReferences()
    {
        RebuildBindingLookup(inventorySlots, inventorySlotLookup);
        RebuildBindingLookup(shopSlots, shopSlotLookup);
    }

    void RebuildBindingLookup(ItemButtonBinding[] bindings, Dictionary<ItemId, ItemButtonBinding> lookup)
    {
        lookup.Clear();

        if (bindings == null)
            return;

        for (int i = 0; i < bindings.Length; i++)
        {
            ItemButtonBinding binding = bindings[i];
            if (binding == null)
                continue;

            binding.ResolveReferences();
            lookup[binding.itemId] = binding;
        }
    }

    void BindUiCallbacks()
    {
        if (uiCallbacksBound)
            return;

        BindButtons(inventorySlots, ToggleEquip);
        BindButtons(shopSlots, OnShopButtonClicked);
        uiCallbacksBound = true;
    }

    void BindButtons(ItemButtonBinding[] bindings, Action<ItemId> callback)
    {
        if (bindings == null)
            return;

        for (int i = 0; i < bindings.Length; i++)
        {
            ItemButtonBinding binding = bindings[i];
            if (binding == null || binding.button == null)
                continue;

            ItemId capturedItemId = binding.itemId;
            binding.button.onClick.AddListener(() => callback(capturedItemId));
        }
    }

    void RefreshUi()
    {
        ResolveUiReferences();
        BindUiCallbacks();

        if (inventoryPanel != null)
            inventoryPanel.SetActive(inventoryOpen);

        if (shopPanel != null)
            shopPanel.SetActive(activeVendor != null);

        if (debugShopFlow && activeVendor != null)
        {
            Debug.Log("[PlayerItemSystem] RefreshUi for vendor '" + activeVendor.DisplayName
                + "'. Shop panel assigned: " + (shopPanel != null)
                + ". Shop title assigned: " + (shopTitleLabel != null) + ".");
        }

        RefreshMoneyLabels();

        if (shopTitleLabel != null)
            shopTitleLabel.text = activeVendor != null ? activeVendor.DisplayName : "Vendor";

        RefreshInventoryButtons();
        RefreshShopButtons();
        ApplyCursorState();
    }

    void RefreshMoneyLabels()
    {
        if (moneyLabels == null)
            return;

        for (int i = 0; i < moneyLabels.Length; i++)
        {
            TMP_Text moneyLabel = moneyLabels[i];
            if (moneyLabel == null)
                continue;

            moneyLabel.text = "$ " + currentMoney;
            moneyLabel.gameObject.SetActive(IsAnyMenuOpen);
        }
    }

    void RefreshInventoryButtons()
    {
        for (int i = 0; i < InventoryOrder.Length; i++)
        {
            ItemId itemId = InventoryOrder[i];
            ItemButtonBinding binding = GetBinding(inventorySlotLookup, itemId);
            ItemDefinition definition = GetDefinition(itemId);

            if (binding == null)
                continue;

            if (definition == null)
            {
                SetBindingState(binding, "Unavailable", false, new Color(0.18f, 0.18f, 0.18f, 0.9f));
                continue;
            }

            bool isOwned = ownedItems.Contains(itemId);
            bool isEquipped = equippedItemId.HasValue && equippedItemId.Value == itemId;

            if (!isOwned)
            {
                SetBindingState(binding, "Empty", false, new Color(0.22f, 0.22f, 0.22f, 0.95f));
            }
            else if (isEquipped)
            {
                SetBindingState(binding, definition.displayName + "\nEquipped\nClick to unequip", true, new Color(0.24f, 0.6f, 0.34f, 0.95f));
            }
            else
            {
                SetBindingState(binding, definition.displayName + "\nClick to equip", true, definition.uiColor);
            }
        }
    }

    void RefreshShopButtons()
    {
        for (int i = 0; i < InventoryOrder.Length; i++)
        {
            ItemId itemId = InventoryOrder[i];
            ItemButtonBinding binding = GetBinding(shopSlotLookup, itemId);
            ItemDefinition definition = GetDefinition(itemId);

            if (binding == null)
                continue;

            if (definition == null)
            {
                SetBindingState(binding, "Unavailable", false, new Color(0.18f, 0.18f, 0.18f, 0.9f));
                continue;
            }

            bool isOwned = ownedItems.Contains(itemId);
            bool canAfford = currentMoney >= definition.price;

            if (isOwned)
            {
                SetBindingState(binding, definition.displayName + "\nOwned", false, new Color(0.24f, 0.38f, 0.24f, 0.95f));
            }
            else if (canAfford)
            {
                SetBindingState(binding, definition.displayName + "\n$ " + definition.price + "\nClick to buy", true, definition.uiColor);
            }
            else
            {
                SetBindingState(binding, definition.displayName + "\n$ " + definition.price + "\nNot enough money", false, new Color(0.35f, 0.18f, 0.18f, 0.95f));
            }
        }
    }

    void SetBindingState(ItemButtonBinding binding, string text, bool interactable, Color color)
    {
        if (binding.label != null)
            binding.label.text = text;

        if (binding.button != null)
        {
            binding.button.interactable = interactable;
            SetButtonColor(binding.button, binding.background, color);
        }
    }

    ItemButtonBinding GetBinding(Dictionary<ItemId, ItemButtonBinding> lookup, ItemId itemId)
    {
        ItemButtonBinding binding;
        lookup.TryGetValue(itemId, out binding);
        return binding;
    }

    ItemDefinition GetDefinition(ItemId itemId)
    {
        ItemDefinition definition;
        itemLookup.TryGetValue(itemId, out definition);
        return definition;
    }

    string GetDefaultDisplayName(ItemId itemId)
    {
        switch (itemId)
        {
            case ItemId.WaterPistol:
                return "Water Pistol";
            case ItemId.AirHorn:
                return "Air Horn";
            default:
                return "Shield";
        }
    }

    void OnShopButtonClicked(ItemId itemId)
    {
        TryBuyItem(itemId);
    }

    void SetButtonColor(Button button, Image background, Color baseColor)
    {
        if (background != null)
            background.color = baseColor;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = baseColor * 1.12f;
        colors.pressedColor = baseColor * 0.92f;
        colors.selectedColor = baseColor * 1.12f;
        colors.disabledColor = new Color(baseColor.r * 0.65f, baseColor.g * 0.65f, baseColor.b * 0.65f, 0.85f);
        button.colors = colors;
    }

    void ApplyCursorState()
    {
        if (IsAnyMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
