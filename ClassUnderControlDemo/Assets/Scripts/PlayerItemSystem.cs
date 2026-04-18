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

    static readonly ItemId[] InventoryOrder =
    {
        ItemId.Shield,
        ItemId.WaterPistol,
        ItemId.AirHorn
    };

    public KeyCode inventoryKey = KeyCode.I;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;

    public int startingMoney = 10;
    public Transform defaultEquipAnchor;
    public float equippedScaleMultiplier = 0.65f;
    public PlayerItemDefinition[] itemDefinitions = new PlayerItemDefinition[3];

    [Header("Item Abilities")]
    public float waterPistolRange = 12f;
    public float waterPistolHitRadius = 0.35f;
    public float airHornStunDuration = 3f;
    public AudioClip airHornClip;

    public GameObject inventoryPanel;
    public TMP_Text[] moneyLabels;
    public PlayerItemButtonBinding[] inventorySlots = new PlayerItemButtonBinding[3];

    public GameObject shopPanel;
    public TMP_Text shopTitleLabel;
    public PlayerItemButtonBinding[] shopSlots = new PlayerItemButtonBinding[3];

    public bool debugShopFlow = true;

    readonly Dictionary<ItemId, PlayerItemDefinition> itemLookup = new Dictionary<ItemId, PlayerItemDefinition>();
    readonly Dictionary<ItemId, PlayerInventoryItem> itemBehaviours = new Dictionary<ItemId, PlayerInventoryItem>();
    readonly HashSet<ItemId> ownedItems = new HashSet<ItemId>();
    readonly Dictionary<ItemId, float> itemDurability = new Dictionary<ItemId, float>();
    readonly Dictionary<ItemId, PlayerItemButtonBinding> inventorySlotLookup = new Dictionary<ItemId, PlayerItemButtonBinding>();
    readonly Dictionary<ItemId, PlayerItemButtonBinding> shopSlotLookup = new Dictionary<ItemId, PlayerItemButtonBinding>();

    int currentMoney;
    ItemId? equippedItemId;
    PlayerInventoryItem equippedItemBehaviour;
    GameObject equippedVisual;
    VendorShop activeVendor;
    bool inventoryOpen;
    bool uiCallbacksBound;
    int shopOpenedFrame = -1;

    public int CurrentMoney => currentMoney;
    public bool IsAnyMenuOpen => inventoryOpen || activeVendor != null;
    public bool IsInventoryOpen => inventoryOpen;
    public bool IsShopOpen => activeVendor != null;
    public ItemId? EquippedItemId => equippedItemId;
    public GameObject EquippedVisual => equippedVisual;
    public bool HasItem(ItemId itemId) => ownedItems.Contains(itemId);
    public float WaterPistolRange => waterPistolRange;
    public float WaterPistolHitRadius => waterPistolHitRadius;
    public float AirHornStunDuration => airHornStunDuration;
    public AudioClip AirHornClip => airHornClip;

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        RefreshUi();
    }

    void OnDisable()
    {
        StopActiveItemUsage();
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

        StopActiveItemUsage();
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

        StopActiveItemUsage();
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
        StopActiveItemUsage();
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

        PlayerItemDefinition definition = GetDefinition(itemId);
        if (definition == null)
            return false;

        if (currentMoney < definition.price)
            return false;

        currentMoney -= definition.price;
        ownedItems.Add(itemId);
        itemDurability[itemId] = GetMaxDurability(itemId);
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

    public bool TryBlockProjectile()
    {
        return equippedItemBehaviour != null && equippedItemBehaviour.TryBlockProjectile();
    }

    public bool TryHandlePrimaryAction(Camera sourceCamera, bool pressedThisFrame, bool heldThisFrame, bool releasedThisFrame)
    {
        if (GameManager.I != null && (GameManager.I.classTimerPaused || GameManager.I.IsClassEnded))
        {
            StopActiveItemUsage();
            return false;
        }

        if (!equippedItemId.HasValue || !ownedItems.Contains(equippedItemId.Value))
        {
            StopActiveItemUsage();
            return false;
        }

        if (equippedItemBehaviour == null)
        {
            StopActiveItemUsage();
            return false;
        }

        return equippedItemBehaviour.TryHandlePrimaryAction(
            sourceCamera,
            pressedThisFrame,
            heldThisFrame,
            releasedThisFrame);
    }

    public bool TryConsumeItemDurability(ItemId itemId, float amount)
    {
        return ConsumeItemDurability(itemId, amount);
    }

    public FireHazard ResolveFireHazard(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        FireHazard fireHazard = hitCollider.GetComponent<FireHazard>();
        if (fireHazard != null)
            return fireHazard;

        fireHazard = hitCollider.GetComponentInParent<FireHazard>();
        if (fireHazard != null)
            return fireHazard;

        return hitCollider.transform.root.GetComponentInChildren<FireHazard>(true);
    }

    void EquipItem(ItemId itemId)
    {
        StopActiveItemUsage();
        UnequipCurrentItem();
        equippedItemId = itemId;
        equippedItemBehaviour = GetItemBehaviour(itemId);

        PlayerItemDefinition definition = GetDefinition(itemId);
        if (definition == null)
            return;

        Transform anchor = definition.equipAnchorOverride != null ? definition.equipAnchorOverride : defaultEquipAnchor;
        if (anchor != null && definition.equipPrefab != null)
        {
            equippedVisual = Instantiate(definition.equipPrefab, anchor);
            equippedVisual.name = definition.equipPrefab.name + "_Equipped";
            equippedVisual.transform.localPosition = definition.localPosition;
            equippedVisual.transform.localRotation = Quaternion.Euler(GetEquippedRotation(itemId, definition.localEulerAngles));
            equippedVisual.transform.localScale = GetEquippedScale(definition.localScale);
            DisablePhysicsOnEquippedItem(equippedVisual);
        }

        if (equippedItemBehaviour != null)
            equippedItemBehaviour.OnEquipped();
    }

    void UnequipCurrentItem()
    {
        StopActiveItemUsage();

        if (equippedItemBehaviour != null)
            equippedItemBehaviour.OnUnequipped();

        equippedItemBehaviour = null;
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
        StopActiveItemUsage();
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

        StopActiveItemUsage();
        activeVendor = null;
        RefreshUi();
    }

    void RebuildLookup()
    {
        itemLookup.Clear();

        if (itemDefinitions != null)
        {
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                PlayerItemDefinition definition = itemDefinitions[i];
                if (definition == null)
                    continue;

                itemLookup[definition.itemId] = definition;
            }
        }

        RebuildItemBehaviours();
    }

    void RebuildItemBehaviours()
    {
        itemBehaviours.Clear();

        if (itemDefinitions != null)
        {
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                PlayerItemDefinition definition = itemDefinitions[i];
                if (definition == null)
                    continue;

                PlayerInventoryItem itemBehaviour = PlayerItemFactory.Create(this, definition);
                if (itemBehaviour != null)
                    itemBehaviours[definition.itemId] = itemBehaviour;
            }
        }

        if (equippedItemId.HasValue)
            equippedItemBehaviour = GetItemBehaviour(equippedItemId.Value);
        else
            equippedItemBehaviour = null;
    }

    void EnsureDefinitionDefaults()
    {
        if (itemDefinitions == null || itemDefinitions.Length != InventoryOrder.Length)
        {
            PlayerItemDefinition[] resized = new PlayerItemDefinition[InventoryOrder.Length];
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
                itemDefinitions[i] = new PlayerItemDefinition();

            itemDefinitions[i].itemId = InventoryOrder[i];

            if (string.IsNullOrWhiteSpace(itemDefinitions[i].displayName))
                itemDefinitions[i].displayName = PlayerItemFactory.GetDefaultDisplayName(InventoryOrder[i]);

            if (itemDefinitions[i].localScale == Vector3.zero)
                itemDefinitions[i].localScale = Vector3.one;

            PlayerItemFactory.ApplyDurabilityDefaults(itemDefinitions[i], InventoryOrder[i]);
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

    PlayerItemButtonBinding[] EnsureBindingArray(PlayerItemButtonBinding[] source)
    {
        if (source == null || source.Length != InventoryOrder.Length)
        {
            PlayerItemButtonBinding[] resized = new PlayerItemButtonBinding[InventoryOrder.Length];
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
                source[i] = new PlayerItemButtonBinding();

            source[i].itemId = InventoryOrder[i];
        }

        return source;
    }

    void ResolveUiReferences()
    {
        RebuildBindingLookup(inventorySlots, inventorySlotLookup);
        RebuildBindingLookup(shopSlots, shopSlotLookup);
    }

    void RebuildBindingLookup(PlayerItemButtonBinding[] bindings, Dictionary<ItemId, PlayerItemButtonBinding> lookup)
    {
        lookup.Clear();

        if (bindings == null)
            return;

        for (int i = 0; i < bindings.Length; i++)
        {
            PlayerItemButtonBinding binding = bindings[i];
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

    void BindButtons(PlayerItemButtonBinding[] bindings, Action<ItemId> callback)
    {
        if (bindings == null)
            return;

        for (int i = 0; i < bindings.Length; i++)
        {
            PlayerItemButtonBinding binding = bindings[i];
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
            PlayerItemButtonBinding binding = GetBinding(inventorySlotLookup, itemId);
            PlayerItemDefinition definition = GetDefinition(itemId);

            if (binding == null)
                continue;

            if (definition == null)
            {
                SetBindingState(binding, "Unavailable", false, new Color(0.18f, 0.18f, 0.18f, 0.9f));
                SetDurabilityState(binding, false, 0f);
                continue;
            }

            bool isOwned = ownedItems.Contains(itemId);
            bool isEquipped = equippedItemId.HasValue && equippedItemId.Value == itemId;
            float durabilityNormalized = isOwned ? GetDurabilityNormalized(itemId) : 0f;

            if (!isOwned)
            {
                SetBindingState(binding, "Empty", false, new Color(0.22f, 0.22f, 0.22f, 0.95f));
                SetDurabilityState(binding, false, 0f);
            }
            else if (isEquipped)
            {
                SetBindingState(binding, definition.displayName + "\nEquipped\nClick to unequip", true, new Color(0.24f, 0.6f, 0.34f, 0.95f));
                SetDurabilityState(binding, true, durabilityNormalized);
            }
            else
            {
                SetBindingState(binding, definition.displayName + "\nClick to equip", true, definition.uiColor);
                SetDurabilityState(binding, true, durabilityNormalized);
            }
        }
    }

    void RefreshShopButtons()
    {
        for (int i = 0; i < InventoryOrder.Length; i++)
        {
            ItemId itemId = InventoryOrder[i];
            PlayerItemButtonBinding binding = GetBinding(shopSlotLookup, itemId);
            PlayerItemDefinition definition = GetDefinition(itemId);

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

    void SetBindingState(PlayerItemButtonBinding binding, string text, bool interactable, Color color)
    {
        if (binding.label != null)
            binding.label.text = text;

        if (binding.button != null)
        {
            binding.button.interactable = interactable;
            SetButtonColor(binding.button, binding.background, color);
        }
    }

    void SetDurabilityState(PlayerItemButtonBinding binding, bool visible, float normalizedValue)
    {
        if (binding == null || binding.durabilityBar == null)
            return;

        binding.durabilityBar.gameObject.SetActive(visible);
        binding.durabilityBar.minValue = 0f;
        binding.durabilityBar.maxValue = 1f;
        binding.durabilityBar.value = Mathf.Clamp01(normalizedValue);

        if (binding.durabilityFill != null)
        {
            Color emptyColor = new Color(0.82f, 0.2f, 0.2f, 0.95f);
            Color fullColor = new Color(0.25f, 0.8f, 0.35f, 0.95f);
            binding.durabilityFill.color = Color.Lerp(emptyColor, fullColor, binding.durabilityBar.value);
        }
    }

    PlayerItemButtonBinding GetBinding(Dictionary<ItemId, PlayerItemButtonBinding> lookup, ItemId itemId)
    {
        PlayerItemButtonBinding binding;
        lookup.TryGetValue(itemId, out binding);
        return binding;
    }

    PlayerInventoryItem GetItemBehaviour(ItemId itemId)
    {
        PlayerInventoryItem itemBehaviour;
        itemBehaviours.TryGetValue(itemId, out itemBehaviour);
        return itemBehaviour;
    }

    PlayerItemDefinition GetDefinition(ItemId itemId)
    {
        PlayerItemDefinition definition;
        itemLookup.TryGetValue(itemId, out definition);
        return definition;
    }

    Vector3 GetEquippedRotation(ItemId itemId, Vector3 baseEuler)
    {
        if (itemId == ItemId.WaterPistol || itemId == ItemId.AirHorn)
            baseEuler.y -= 90f;

        return baseEuler;
    }

    Vector3 GetEquippedScale(Vector3 baseScale)
    {
        float multiplier = Mathf.Max(0.01f, equippedScaleMultiplier);
        return baseScale * multiplier;
    }

    void OnShopButtonClicked(ItemId itemId)
    {
        TryBuyItem(itemId);
    }

    float GetCurrentDurability(ItemId itemId)
    {
        float currentDurability;
        if (itemDurability.TryGetValue(itemId, out currentDurability))
            return currentDurability;

        return GetMaxDurability(itemId);
    }

    float GetMaxDurability(ItemId itemId)
    {
        PlayerItemDefinition definition = GetDefinition(itemId);
        return definition != null ? Mathf.Max(0f, definition.maxDurability) : 0f;
    }

    float GetDurabilityNormalized(ItemId itemId)
    {
        float maxDurability = GetMaxDurability(itemId);
        if (maxDurability <= 0f)
            return 0f;

        return Mathf.Clamp01(GetCurrentDurability(itemId) / maxDurability);
    }

    bool ConsumeItemDurability(ItemId itemId, float amount)
    {
        if (!ownedItems.Contains(itemId))
            return false;

        float currentDurability = GetCurrentDurability(itemId);
        if (currentDurability <= 0f)
        {
            RemoveOwnedItem(itemId);
            return false;
        }

        float nextDurability = Mathf.Max(0f, currentDurability - Mathf.Max(0f, amount));
        itemDurability[itemId] = nextDurability;

        if (nextDurability <= 0f)
            RemoveOwnedItem(itemId);
        else
            RefreshUi();

        return true;
    }

    void RemoveOwnedItem(ItemId itemId)
    {
        bool wasOwned = ownedItems.Remove(itemId);
        itemDurability.Remove(itemId);

        if (equippedItemId.HasValue && equippedItemId.Value == itemId)
            UnequipCurrentItem();

        if (wasOwned)
            RefreshUi();
    }

    void StopActiveItemUsage()
    {
        if (equippedItemBehaviour != null)
            equippedItemBehaviour.StopActiveUse();
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
